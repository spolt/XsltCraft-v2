using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <param name="Targets">v1: id listesi (betik hepsine yazar). v2: önce yuva, sonra yedek id'ler.</param>
/// <param name="Slot">v2'de yuva kimliği.</param>
/// <param name="ExtraSlots">v2 <c>xs</c>: görselin ayrıca kopyalandığı yuvalar (en fazla <see cref="OverridesBlockParser.MaxExtraSlots"/>).</param>
/// <param name="Keep">v2 <c>keep</c>: temadaki aynı tür görsel gizlenmez.</param>
internal sealed record ParsedImage(ThemeAssetKind? Kind, string? Slot, IReadOnlyList<string> Targets, string DataUrl,
    int? Width, int? Height, ThemeAlignment Alignment, IReadOnlyList<string>? ExtraSlots = null, bool Keep = false)
{
    public IReadOnlyList<string> ExtraSlots { get; init; } = ExtraSlots ?? [];
}

/// <summary>v2 <c>k:"free"</c>: türü olmayan, yalnız yuvasına yerleşen serbest görsel.</summary>
internal sealed record ParsedFreeImage(string Slot, string DataUrl, int? Width, int? Height, ThemeAlignment Alignment);

/// <param name="Hide">v2 <c>hide</c>: kullanıcının görsel eklemeden kaldırdığı temadaki görsel türleri.</param>
/// <param name="FreeImages">v2 serbest görseller (en fazla <see cref="OverridesBlockParser.MaxFreeImages"/>).</param>
internal sealed record ParsedOverrides(int Version, IReadOnlyList<ParsedImage> Images, string? BankTableXml, int BankRows,
    bool Recognized, string? Reason, IReadOnlyList<ThemeAssetKind>? Hide = null, IReadOnlyList<ParsedFreeImage>? FreeImages = null)
{
    public IReadOnlyList<ThemeAssetKind> Hide { get; init; } = Hide ?? [];
    public IReadOnlyList<ParsedFreeImage> FreeImages { get; init; } = FreeImages ?? [];
}

/// <summary>
/// Kullanıcı görsel bloğunun içeriğini ayrıştırır. Yalnız bizim ürettiğimiz biçimler tanınır:
/// v1 (<c>setImg([...ids], 'data:…', w, h, 'align')</c> çağrıları — Tema Kullan'ın eski çıktısı) ve
/// v2 (<c>script[data-xc-overrides-data="2"]</c> JSON) + isteğe bağlı banka tablosu. Tanınmayan her şey
/// "korunamaz" sayılır; hiçbir içerik sessizce düşürülmez.
/// </summary>
internal static partial class OverridesBlockParser
{
    public const int MaxExtraSlots = 3;

    /// <summary>Blokta en fazla serbest görsel; fazlası tanınmaz.</summary>
    public const int MaxFreeImages = 1;

    /// <summary>Ayrıştırılan blok iç metninin üst sınırı (karakter); aşan blok tanınmaz.</summary>
    public const int MaxInnerChars = 2 * 1024 * 1024;

    // Görsel boyutu: compose doğrulamasıyla aynı aralık (10–1200 piksel).
    private const int MinImageSize = 10;
    private const int MaxImageSize = 1200;

    private const int MatchTimeoutMs = 2000;

    [GeneratedRegex(
        @"setImg\(\[\s*('[A-Za-z_][\w-]*'(?:\s*,\s*'[A-Za-z_][\w-]*')*)\s*\]\s*,\s*'(data:image/(?:png|jpe?g);base64,[A-Za-z0-9+/]+={0,2})'\s*,\s*(null|\d{1,4})\s*,\s*(null|\d{1,4})\s*,\s*'(left|center|right)'\s*\)\s*;",
        RegexOptions.CultureInvariant, MatchTimeoutMs)]
    private static partial Regex V1Call();

    [GeneratedRegex(@"'([A-Za-z_][\w-]*)'", RegexOptions.CultureInvariant, MatchTimeoutMs)]
    private static partial Regex QuotedId();

    public static ParsedOverrides Parse(string inner)
    {
        if (inner.Length > MaxInnerChars)
            return Unrecognized(0, "Görsel bloğu çok büyük.");
        try
        {
            return ParseCore(inner);
        }
        catch (RegexMatchTimeoutException)
        {
            return Unrecognized(0, "Görsel bloğu okunamadı.");
        }
    }

    private static ParsedOverrides ParseCore(string inner)
    {
        var images = new List<ParsedImage>();
        var hide = new List<ThemeAssetKind>();
        var free = new List<ParsedFreeImage>();
        var version = 0;

        var scan = OverridesScriptScanner.Scan(inner);
        if (scan is null)
            return Unrecognized(version, "Blokta kapanmamış bir betik var.");

        foreach (var (attrs, body) in scan.Value.Scripts)
        {

            if (attrs.Contains("data-xc-overrides-data", StringComparison.Ordinal))
            {
                version = 2;
                if (!TryParseV2(body, images, hide, free))
                    return Unrecognized(version, "Blokdaki görsel verisi (v2) okunamadı.");
            }
            else if (attrs.Contains("data-xc-overrides", StringComparison.Ordinal))
            {
                version = 2; // çalışma kodu — içeriği bizim; yeniden üretilir
            }
            else if (body.Contains("function setImg(", StringComparison.Ordinal))
            {
                version = 1;
                var calls = V1Call().Matches(body);
                var expected = CountOf(body, "setImg(") - 1; // tanım hariç
                if (calls.Count != expected)
                    return Unrecognized(version, "Blokta tanınmayan ya da desteklenmeyen (ör. SVG) bir görsel çağrısı var.");

                foreach (Match c in calls)
                {
                    var ids = QuotedId().Matches(c.Groups[1].Value).Select(m => m.Groups[1].Value).ToList();
                    var (w, h) = (ParseInt(c.Groups[3].Value), ParseInt(c.Groups[4].Value));
                    if (!SizeInRange(w) || !SizeInRange(h))
                        return Unrecognized(version, "Blokta geçersiz boyutlu bir görsel var.");
                    images.Add(new ParsedImage(KindOf(ids), null, ids, c.Groups[2].Value, w, h, ParseAlign(c.Groups[5].Value)));
                }
            }
            else
            {
                return Unrecognized(version, "Blokta tanınmayan bir betik var.");
            }
        }

        var rest = scan.Value.Rest.Trim();
        if (rest.Length == 0)
            return new ParsedOverrides(version, images, null, 0, true, null, hide, free);

        var rows = BankRows(rest);
        return rows is null
            ? Unrecognized(version, "Blokta tanınmayan bir içerik var (banka tablosu dışında).")
            : new ParsedOverrides(version, images, rest, rows.Value, true, null, hide, free);
    }

    private static bool TryParseV2(string json, List<ParsedImage> images, List<ThemeAssetKind> hide, List<ParsedFreeImage> free)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("v", out var v) || v.GetInt32() != 2 || !root.TryGetProperty("items", out var items))
                return false;

            foreach (var item in items.EnumerateArray())
            {
                if (item.GetProperty("k").GetString() == FreeKind)
                {
                    if (free.Count == MaxFreeImages || !TryFree(item, out var parsedFree))
                        return false;
                    free.Add(parsedFree);
                    continue;
                }

                var kind = KindFromJson(item.GetProperty("k").GetString());
                if (kind is null || !TryExtraSlots(item, out var extra) || !TryKeep(item, out var keep))
                    return false;

                if (!TrySlot(item, out var slot) || !TrySize(item, "w", out var w) || !TrySize(item, "h", out var h))
                    return false;
                var targets = LegacyHolderIds.For(kind.Value).ToList();
                images.Add(new ParsedImage(kind, slot, targets, item.GetProperty("src").GetString() ?? "",
                    w, h, ParseAlign(item.GetProperty("a").GetString() ?? "left"), extra, keep));
            }
            return TryHide(root, hide);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private const string FreeKind = "free";

    // Serbest görsel: yuva zorunlu, yedek id yok, xs/keep yok. existing-* yuvası (boşalmış görsel yeri) okunur;
    // yerleşimde metne göre denetlenir.
    private static bool TryFree(JsonElement item, out ParsedFreeImage image)
    {
        image = null!;
        if (item.TryGetProperty("xs", out _) || item.TryGetProperty("keep", out _))
            return false;
        if (item.TryGetProperty("fb", out var fb) && fb.ValueKind != JsonValueKind.Null
            && (fb.ValueKind != JsonValueKind.Array || fb.GetArrayLength() != 0))
            return false;
        if (!TrySlot(item, out var slot) || slot is null)
            return false;
        if (!TrySize(item, "w", out var w) || !TrySize(item, "h", out var h))
            return false;
        image = new ParsedFreeImage(slot, item.GetProperty("src").GetString() ?? "", w, h,
            ParseAlign(item.GetProperty("a").GetString() ?? "left"));
        return true;
    }

    private static ThemeAssetKind? KindFromJson(string? value) => value switch
    {
        "logo" => ThemeAssetKind.Logo,
        "signature" => ThemeAssetKind.Signature,
        _ => null,
    };

    // xs: yuva kimliği dizisi; biçim dışı kimlik ya da tavan aşımı tanınmaz.
    private static bool TryExtraSlots(JsonElement item, out List<string> extra)
    {
        extra = [];
        if (!item.TryGetProperty("xs", out var xs) || xs.ValueKind == JsonValueKind.Null)
            return true;
        if (xs.ValueKind != JsonValueKind.Array || xs.GetArrayLength() > MaxExtraSlots)
            return false;
        foreach (var s in xs.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.String || s.GetString() is not { } id || !ThemeSlotMarkers.SlotIdPattern.IsMatch(id))
                return false;
            extra.Add(id);
        }
        return true;
    }

    // slot: yoksa ya da null ise yuvasız; varsa yuva kimliği biçiminde olmalı.
    private static bool TrySlot(JsonElement item, out string? slot)
    {
        slot = null;
        if (!item.TryGetProperty("slot", out var s) || s.ValueKind == JsonValueKind.Null)
            return true;
        if (s.ValueKind != JsonValueKind.String || s.GetString() is not { } id || !ThemeSlotMarkers.SlotIdPattern.IsMatch(id))
            return false;
        slot = id;
        return true;
    }

    // w / h: yoksa ya da null ise otomatik; varsa aralıkta tam sayı olmalı.
    private static bool TrySize(JsonElement item, string name, out int? size)
    {
        size = null;
        if (!item.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return true;
        if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out var value) || !SizeInRange(value))
            return false;
        size = value;
        return true;
    }

    private static bool SizeInRange(int? size) => size is null or (>= MinImageSize and <= MaxImageSize);

    private static bool TryKeep(JsonElement item, out bool keep)
    {
        keep = false;
        if (!item.TryGetProperty("keep", out var k) || k.ValueKind == JsonValueKind.Null)
            return true;
        if (k.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        keep = k.GetBoolean();
        return true;
    }

    // hide: "logo" | "signature" dizisi; bilinmeyen değer tanınmaz (sessizce düşürülmez).
    private static bool TryHide(JsonElement root, List<ThemeAssetKind> hide)
    {
        if (!root.TryGetProperty("hide", out var h) || h.ValueKind == JsonValueKind.Null)
            return true;
        if (h.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var v in h.EnumerateArray())
        {
            if (v.ValueKind != JsonValueKind.String || KindFromJson(v.GetString()) is not { } kind)
                return false;
            if (!hide.Contains(kind))
                hide.Add(kind);
        }
        return true;
    }

    // Banka tablosu: div > table > thead/tbody > tr > th/td, yalnız style niteliği ve düz metin. Satır sayısı ya da null.
    private static int? BankRows(string fragment)
    {
        XElement root;
        try
        {
            using var reader = XmlReader.Create(new StringReader(fragment), SecureXslt.ReaderSettings());
            root = XElement.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        if (root.Name.LocalName != "div" || root.Elements().Count() != 1 || root.Elements().Single().Name.LocalName != "table")
            return null;

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "div", "table", "thead", "tbody", "tr", "th", "td" };
        foreach (var e in root.DescendantsAndSelf())
        {
            if (e.Name.Namespace != XNamespace.None || !allowed.Contains(e.Name.LocalName))
                return null;
            if (e.Attributes().Any(a => a.Name.LocalName != "style"))
                return null;
        }
        return root.Descendants("tbody").Elements("tr").Count();
    }

    private static ThemeAssetKind? KindOf(IReadOnlyList<string> ids) =>
        ids.Select(LegacyHolderIds.KindOf).FirstOrDefault(k => k is not null);

    private static int CountOf(string s, string token)
    {
        var n = 0;
        for (var i = s.IndexOf(token, StringComparison.Ordinal); i >= 0; i = s.IndexOf(token, i + token.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

    private static int? ParseInt(string s) => s == "null" ? null : int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    public static ThemeAlignment ParseAlign(string s) => s switch
    {
        "center" => ThemeAlignment.Center,
        "right" => ThemeAlignment.Right,
        _ => ThemeAlignment.Left,
    };

    private static ParsedOverrides Unrecognized(int version, string reason) => new(version, [], null, 0, false, reason);
}
