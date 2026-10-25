using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;

namespace XsltCraft.Application.ThemeSlots.Compose;

public sealed record ThemeAssetInput(ThemeAssetKind Kind, string? Slot, string DataUrl, int? Width, int? Height, ThemeAlignment Alignment);

public sealed record BankInfoInput(string BankName, string Iban);

/// <param name="CatalogDefaults">Temanın varsayılan yuvaları — istenen yuva yoksa kullanılır.</param>
/// <param name="ConditionalSlots">Her faturada çıkmayabilecek yuvalar (uyarı için).</param>
/// <param name="HideThemeImages">Kullanıcının kendi görselini eklemeden kaldırdığı temadaki görsel türleri — yalnız
/// admin'in "kullanıcı kaldırabilir" dediği görseller (<c>data-xc-removable</c>) gizlenir.</param>
public sealed record ThemeComposeInput(
    IReadOnlyList<ThemeAssetInput> Assets,
    IReadOnlyList<BankInfoInput> BankInfo,
    IReadOnlyDictionary<ThemeAssetKind, string>? CatalogDefaults = null,
    IReadOnlySet<string>? ConditionalSlots = null,
    IReadOnlyCollection<ThemeAssetKind>? HideThemeImages = null);

public sealed record ThemeComposeWarning(string Code, string Message, ThemeAssetKind? Kind = null);

public sealed record ThemeComposeResult(bool Success, string Xslt, IReadOnlyList<ThemeComposeWarning> Warnings,
    IReadOnlyDictionary<ThemeAssetKind, string?> AppliedSlots, int AddedImageBytes, string? Error);

/// <summary>
/// Kullanıcının logo/imza/IBAN bilgisini temaya ekleyen tek kaynak: önizleme, indirme ve kaydetme aynı metni
/// kullanır. Görseller XSLT'nin sonuna eklenen <c>DEV-MODE-OVERRIDES</c> bloğundaki JSON veridir; sabit çalışma
/// betiği onları <c>data-xc-slot</c> yuvalarına (yoksa eski id'lere) yerleştirir. Tüm değerler beyaz listeden
/// geçer; JSON'da <c>&lt; &gt; &amp;</c> bulunursa kapalı başarısızlık.
/// </summary>
public interface IThemeAssetComposer
{
    ThemeComposeResult Compose(string xslt, ThemeComposeInput input);
}

/// <remarks>
/// <para><b>Çalışma betiği (ThemeAssetRuntime.js, v2):</b> veri <c>script[data-xc-overrides-data]</c> içindeki JSON'dur.
/// Hedef <c>data-xc-slot</c> yuvası; bulunamazsa yedek id'ler (eski temalar). Yuva temadaki bir görselse
/// (<c>data-xc-placeholder</c>) kullanıcının görseli onun yerini alır (kutu korunur, görsel oranı bozulmadan sığar);
/// aynı türden görsel başka bir yere yerleştiyse temadakiler gizlenir (yerleri korunur), hiçbir yere yerleşemediyse
/// temadaki görsel kalır. Yedek id bir <c>img</c> ise (analiz edilmemiş eski tema) içine eklenmez, yerini alır; ikinci
/// görsel ardına eklenir. <c>data.hide</c>: kullanıcının görsel eklemeden kaldırdığı türler — yalnız
/// <c>data-xc-removable</c> taşıyanlar gizlenir.</para>
/// <para>Betik XSLT'ye olduğu gibi gömülür: <c>&lt; &gt; &amp;</c> karakterleri ve açıklama yorumu içermez
/// (sahip kuralı — üretilen çıktıda yorum yok; açıklamalar burada tutulur).</para>
/// </remarks>
public sealed class ThemeAssetComposer(IThemeImageValidator images) : IThemeAssetComposer
{
    public const int LargeAssetsWarningBytes = 150 * 1024;

    private static readonly Lazy<string> Runtime = new(LoadRuntime);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // base64'teki '+' ve '/' kaçışlanmasın (~%8 şişme); güvenlik beyaz liste + aşağıdaki assert ile.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex XslPrefix = new(@"xmlns:([A-Za-z_][\w.-]*)=""http://www\.w3\.org/1999/XSL/Transform""", RegexOptions.CultureInvariant);

    public ThemeComposeResult Compose(string xslt, ThemeComposeInput input)
    {
        var warnings = new List<ThemeComposeWarning>();
        var applied = new Dictionary<ThemeAssetKind, string?>();

        var scan = OverridesBlockText.Scan(xslt);
        if (!scan.WellFormed)
            return Fail(xslt, "Şablondaki görsel bloğu işaretleri bozuk (eşleşmeyen ya da birden fazla blok). Lütfen düzeltin.");

        // Yuva kümesi, eski blok hariç metinden.
        var withoutBlock = scan.Blocks.Count == 1 ? OverridesBlockText.RemoveAll(xslt) : xslt;
        var slots = ThemeSlotMarkers.SlotIdsIn(withoutBlock);

        var items = new List<object>();
        var addedBytes = 0;
        foreach (var asset in input.Assets)
        {
            var check = images.Check(asset.DataUrl);
            if (!check.Valid)
                return Fail(xslt, $"{KindLabel(asset.Kind)}: {check.Error}");
            addedBytes += check.DecodedBytes;

            var slot = ResolveSlot(asset, slots, input, warnings);
            applied[asset.Kind] = slot;

            items.Add(new
            {
                k = asset.Kind == ThemeAssetKind.Logo ? "logo" : "signature",
                slot,
                fb = LegacyHolderIds.For(asset.Kind),
                src = asset.DataUrl,
                w = asset.Width,
                h = asset.Height,
                a = asset.Alignment.ToString().ToLowerInvariant(),
            });
        }

        // Kendi görseli olan tür zaten temadakinin yerini alır; gizleme yalnız görselsiz türler için.
        var hide = (input.HideThemeImages ?? []).Distinct()
            .Where(k => input.Assets.All(a => a.Kind != k) && slots.Contains(ExistingSlotId(k)))
            .Select(k => k == ThemeAssetKind.Logo ? "logo" : "signature")
            .ToList();

        var banks = input.BankInfo.Where(b => !string.IsNullOrWhiteSpace(b.BankName) || !string.IsNullOrWhiteSpace(b.Iban)).ToList();
        if (items.Count == 0 && hide.Count == 0 && banks.Count == 0)
            return Done(withoutBlock, warnings, applied, 0);

        var parts = new List<string>();
        if (items.Count > 0 || hide.Count > 0)
        {
            var json = JsonSerializer.Serialize(new { v = 2, items, hide }, JsonOptions);
            if (json.IndexOfAny(['<', '>', '&']) >= 0)
                return Fail(xslt, "Görsel verisi güvenli biçimde gömülemedi.");
            parts.Add($@"<script type=""application/json"" data-xc-overrides-data=""2"">{json}</script>");
            parts.Add($@"<script data-xc-overrides=""2"">{Runtime.Value}</script>");
        }
        if (banks.Count > 0)
            parts.Add(BankTable(banks));

        var prefix = XslPrefix.Match(xslt) is { Success: true } m ? m.Groups[1].Value : "xsl";
        var composed = OverridesBlockText.Upsert(xslt, OverridesBlockText.Wrap(string.Join("\n", parts)), prefix);
        if (composed is null)
            return Fail(xslt, "Görsel bloğu eklenemedi.");

        if (XsltSafety.FindThreat(composed) is { } threat)
            return Fail(xslt, threat);

        if (addedBytes > LargeAssetsWarningBytes)
            warnings.Add(new ThemeComposeWarning("assets-large",
                $"Eklenen görseller şablonu yaklaşık {addedBytes / 1024} KB büyütüyor; GİB için şablonun ~250 KB altında kalması önerilir."));

        return Done(composed, warnings, applied, addedBytes);
    }

    private static string? ResolveSlot(ThemeAssetInput asset, IReadOnlyCollection<string> slots, ThemeComposeInput input,
        List<ThemeComposeWarning> warnings)
    {
        if (asset.Slot is not null && ThemeSlotMarkers.SlotIdPattern.IsMatch(asset.Slot) && slots.Contains(asset.Slot))
        {
            if (input.ConditionalSlots?.Contains(asset.Slot) == true)
                warnings.Add(new ThemeComposeWarning("slot-conditional",
                    $"{KindLabel(asset.Kind)} için seçilen konum bazı faturalarda görünmeyebilir.", asset.Kind));
            return asset.Slot;
        }

        if (input.CatalogDefaults?.TryGetValue(asset.Kind, out var fallback) == true && slots.Contains(fallback))
        {
            if (asset.Slot is not null)
                warnings.Add(new ThemeComposeWarning("slot-not-found",
                    $"{KindLabel(asset.Kind)} için seçilen konum bu şablonda yok; varsayılan konum kullanıldı.", asset.Kind));
            return fallback;
        }

        if (asset.Slot is not null)
            warnings.Add(new ThemeComposeWarning("legacy-fallback",
                $"{KindLabel(asset.Kind)} için seçilen konum bu şablonda yok; şablonun eski logo/imza alanı kullanılacak.", asset.Kind));
        return null;
    }

    // Bugünkü banka tablosuyla aynı HTML; XElement serileştirmesi metni otomatik kaçışlar.
    private static string BankTable(List<BankInfoInput> banks)
    {
        const string th = "text-align:left;padding:6px 10px;background:#f5f5f5;border:1px solid #ddd";
        const string td = "padding:6px 10px;border:1px solid #ddd";
        var table = new XElement("div",
            new XAttribute("style", "font-family:Arial,sans-serif;font-size:12px;margin:16px 0;page-break-inside:avoid"),
            new XElement("table", new XAttribute("style", "width:100%;border-collapse:collapse"),
                new XElement("thead", new XElement("tr",
                    new XElement("th", new XAttribute("style", th), "Banka Adı"),
                    new XElement("th", new XAttribute("style", th), "IBAN"))),
                new XElement("tbody", banks.Select(b => new XElement("tr",
                    new XElement("td", new XAttribute("style", td), b.BankName),
                    new XElement("td", new XAttribute("style", td + ";font-family:monospace"), b.Iban))))));
        return table.ToString(SaveOptions.DisableFormatting);
    }

    private static string ExistingSlotId(ThemeAssetKind kind) => kind == ThemeAssetKind.Logo ? "existing-logo" : "existing-signature";

    private static string KindLabel(ThemeAssetKind kind) => kind == ThemeAssetKind.Logo ? "Logo" : "İmza / Kaşe";

    private static ThemeComposeResult Done(string xslt, List<ThemeComposeWarning> warnings,
        Dictionary<ThemeAssetKind, string?> applied, int bytes) => new(true, xslt, warnings, applied, bytes, null);

    private static ThemeComposeResult Fail(string xslt, string error) =>
        new(false, xslt, [], new Dictionary<ThemeAssetKind, string?>(), 0, error);

    private static string LoadRuntime()
    {
        var asm = typeof(ThemeAssetComposer).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith(".ThemeSlots.Compose.ThemeAssetRuntime.js", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var code = reader.ReadToEnd().Trim();
        if (code.IndexOfAny(['<', '>', '&']) >= 0)
            throw new InvalidOperationException("ThemeAssetRuntime.js XSLT'ye gömülemez: '<', '>' ya da '&' içeriyor.");
        if (code.Contains("/*", StringComparison.Ordinal) || code.Contains("//", StringComparison.Ordinal))
            throw new InvalidOperationException("ThemeAssetRuntime.js açıklama yorumu içeremez (üretilen çıktıda yorum yok).");
        return code;
    }
}
