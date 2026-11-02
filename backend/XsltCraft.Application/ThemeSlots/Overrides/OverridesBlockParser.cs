using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <param name="Targets">v1: id listesi (betik hepsine yazar). v2: önce yuva, sonra yedek id'ler.</param>
/// <param name="Slot">v2'de yuva kimliği.</param>
internal sealed record ParsedImage(ThemeAssetKind? Kind, string? Slot, IReadOnlyList<string> Targets, string DataUrl,
    int? Width, int? Height, ThemeAlignment Alignment);

internal sealed record ParsedOverrides(int Version, IReadOnlyList<ParsedImage> Images, string? BankTableXml, int BankRows,
    bool Recognized, string? Reason);

/// <summary>
/// Kullanıcı görsel bloğunun içeriğini ayrıştırır. Yalnız bizim ürettiğimiz biçimler tanınır:
/// v1 (<c>setImg([...ids], 'data:…', w, h, 'align')</c> çağrıları — Tema Kullan'ın eski çıktısı) ve
/// v2 (<c>script[data-xc-overrides-data="2"]</c> JSON) + isteğe bağlı banka tablosu. Tanınmayan her şey
/// "korunamaz" sayılır; hiçbir içerik sessizce düşürülmez.
/// </summary>
internal static partial class OverridesBlockParser
{
    [GeneratedRegex(@"<script\b([^>]*)>(.*?)</script>", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Script();

    [GeneratedRegex(
        @"setImg\(\[\s*('[A-Za-z_][\w-]*'(?:\s*,\s*'[A-Za-z_][\w-]*')*)\s*\]\s*,\s*'(data:image/(?:png|jpe?g);base64,[A-Za-z0-9+/]+={0,2})'\s*,\s*(null|\d{1,4})\s*,\s*(null|\d{1,4})\s*,\s*'(left|center|right)'\s*\)\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex V1Call();

    [GeneratedRegex(@"'([A-Za-z_][\w-]*)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedId();

    public static ParsedOverrides Parse(string inner)
    {
        var images = new List<ParsedImage>();
        var version = 0;

        foreach (Match script in Script().Matches(inner))
        {
            var attrs = script.Groups[1].Value;
            var body = script.Groups[2].Value;

            if (attrs.Contains("data-xc-overrides-data", StringComparison.Ordinal))
            {
                version = 2;
                if (!TryParseV2(body, images))
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
                    images.Add(new ParsedImage(KindOf(ids), null, ids, c.Groups[2].Value,
                        ParseInt(c.Groups[3].Value), ParseInt(c.Groups[4].Value), ParseAlign(c.Groups[5].Value)));
                }
            }
            else
            {
                return Unrecognized(version, "Blokta tanınmayan bir betik var.");
            }
        }

        var rest = Script().Replace(inner, string.Empty).Trim();
        if (rest.Length == 0)
            return new ParsedOverrides(version, images, null, 0, true, null);

        var rows = BankRows(rest);
        return rows is null
            ? Unrecognized(version, "Blokta tanınmayan bir içerik var (banka tablosu dışında).")
            : new ParsedOverrides(version, images, rest, rows.Value, true, null);
    }

    private static bool TryParseV2(string json, List<ParsedImage> images)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("v", out var v) || v.GetInt32() != 2 || !root.TryGetProperty("items", out var items))
                return false;

            foreach (var item in items.EnumerateArray())
            {
                var kind = item.GetProperty("k").GetString() switch
                {
                    "logo" => ThemeAssetKind.Logo,
                    "signature" => ThemeAssetKind.Signature,
                    _ => (ThemeAssetKind?)null,
                };
                if (kind is null)
                    return false;

                var slot = item.TryGetProperty("slot", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                var targets = LegacyHolderIds.For(kind.Value).ToList();
                images.Add(new ParsedImage(kind, slot, targets, item.GetProperty("src").GetString() ?? "",
                    OptionalInt(item, "w"), OptionalInt(item, "h"), ParseAlign(item.GetProperty("a").GetString() ?? "left")));
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
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

    private static int? OptionalInt(JsonElement item, string name) =>
        item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    public static ThemeAlignment ParseAlign(string s) => s switch
    {
        "center" => ThemeAlignment.Center,
        "right" => ThemeAlignment.Right,
        _ => ThemeAlignment.Left,
    };

    private static ParsedOverrides Unrecognized(int version, string reason) => new(version, [], null, 0, false, reason);
}
