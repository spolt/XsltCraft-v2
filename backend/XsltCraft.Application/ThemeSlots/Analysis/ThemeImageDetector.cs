using System.Text.RegularExpressions;
using System.Xml.Linq;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>Temaya gömülü, yuva yapılabilecek statik görsel.</summary>
internal sealed record DetectedImage(int Index, XElement Element, ThemeImageRole Suggested, string Reason, int Line, string? PreviewDataUrl)
{
    public ThemeImageInfo ToInfo() => new(Index, Suggested, Reason, Line, PreviewDataUrl);
}

/// <summary>
/// Temadaki statik görselleri (<c>&lt;img src="data:…"&gt;</c>) bulur ve rol önerir. GİB logosu, dinamik kaynaklı
/// görseller (barkod/karekod hedefleri, AVT), korunan eski blok görselleri ve tekrarlayan döngüdeki görseller
/// hariçtir. Öneri konuma dayanır — <c>alt</c>/<c>id</c> güvenilmez (kaşe görsellerinde <c>alt="company_logo"</c>,
/// firma logolarında <c>alt="E-fatura Logo"</c> sık görülür); son kararı admin verir.
/// </summary>
internal static class ThemeImageDetector
{
    private static readonly Regex SignatureWords = new(@"imza|ka[sş]e|m[uü]h[uü]r|signature|stamp",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PreviewSource = new(@"^data:image/(png|jpe?g|gif|webp|bmp);base64,[A-Za-z0-9+/]+=*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CssSize = new(@"(?:^|;)\s*(width|height)\s*:\s*(\d+)\s*px", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>Kenarı bundan büyük görsel (şerit, filigran) logo/imza önerilmez.</summary>
    private const int LargeImagePx = 400;

    public static List<DetectedImage> Detect(ThemeDocument d, ThemeAnchors a, InsertionPointResolver resolver,
        List<ThemeSlotWarning>? warnings = null)
    {
        var result = new List<DetectedImage>();
        foreach (var img in d.Body.Descendants().Where(e => IsLiteral(e, "img") && !IsExcluded(e)))
        {
            if (ReferenceEquals(img, a.GibLogoImage))
                continue;
            var src = (string?)img.Attribute("src");
            if (string.IsNullOrWhiteSpace(src) || src.Contains('{'))
                continue; // barkod/karekod hedefi ya da çalışma anında hesaplanan kaynak
            if (img.Ancestors().Any(x => IsLiteral(x) && x.Attribute(ThemeSlotMarkers.OccupiedAttribute) is not null))
                continue; // "temanın parçası olarak koru" ile gömülmüş eski görsel

            var line = d.Map.LineOf(d.Map.Span(img).Start);
            if (!resolver.IsValidContext(img))
            {
                warnings?.Add(new ThemeSlotWarning("image-not-slottable",
                    $"Satır {line}'deki görsel tekrarlanan bir bölümde; logo/imza konumu olarak kullanılamaz."));
                continue;
            }

            var (role, reason) = Suggest(d, a, img);
            var compact = Spaces.Replace(src, string.Empty);
            result.Add(new DetectedImage(result.Count, img, role, reason, line,
                PreviewSource.IsMatch(compact) ? compact : null));
        }
        return result;
    }

    private static (ThemeImageRole Role, string Reason) Suggest(ThemeDocument d, ThemeAnchors a, XElement img)
    {
        if (IsLarge(img))
            return (ThemeImageRole.Design, "Büyük görsel — tasarım öğesi olabilir");

        var hints = string.Join(' ', new[] { "id", "alt", "class", "title" }.Select(n => (string?)img.Attribute(n) ?? ""));
        if (SignatureWords.IsMatch(hints))
            return (ThemeImageRole.Signature, "Adı imza/kaşe olduğunu gösteriyor");

        if (InCustomerRow(a, img))
            return (ThemeImageRole.Signature, "Alıcı bilgilerinin yanında");

        if (HeaderTable(d, a) is { } header && img.Ancestors().Contains(header))
            return (ThemeImageRole.Logo, "Başlıkta (satıcı / GİB logosu / karekod ile aynı bölümde)");

        var at = d.Map.Span(img).Start;
        int? Start(XElement? e) => e is null ? null : d.Map.Span(e).Start;

        var middle = new[] { Start(a.Customer), Start(a.DocInfo), Start(a.Lines) }.OfType<int>().DefaultIfEmpty(int.MaxValue).Min();
        if (at < middle)
            return (ThemeImageRole.Logo, "Belgenin üst bölümünde");

        if (Start(a.Lines) is { } lines && at > lines)
            return (ThemeImageRole.Signature, "Belgenin alt bölümünde (toplamlar / notlar)");

        return (ThemeImageRole.Design, "Konumundan rolü çıkarılamadı");
    }

    // Alıcı hücresiyle aynı satırdaki başka bir hücrede (tipik kaşe/imza yeri).
    private static bool InCustomerRow(ThemeAnchors a, XElement img)
    {
        if (a.Customer is null)
            return false;
        var cell = LiteralAncestorOrSelf(a.Customer, "td") ?? LiteralAncestorOrSelf(a.Customer, "th");
        var row = cell is null ? null : LiteralAncestor(cell, "tr");
        if (cell is null || row is null || img.Ancestors().Contains(cell))
            return false;
        var imgCell = img.Ancestors().FirstOrDefault(x => IsLiteral(x, "td") || IsLiteral(x, "th"));
        return imgCell is not null && ReferenceEquals(LiteralAncestor(imgCell, "tr"), row);
    }

    // Satıcı, GİB logosu ya da karekodu içeren en dıştaki tablo.
    private static XElement? HeaderTable(ThemeDocument d, ThemeAnchors a)
    {
        var anchors = new[] { a.Seller, a.GibLogo, a.Qr }.OfType<XElement>().ToList();
        if (anchors.Count == 0)
            return null;
        return d.Body.Descendants().FirstOrDefault(e => IsLiteral(e, "table")
            && anchors.Any(x => x.Ancestors().Contains(e))
            && (a.Customer is null || !a.Customer.Ancestors().Contains(e))
            && (a.Lines is null || !ReferenceEquals(a.Lines, e) && !a.Lines.Ancestors().Contains(e)));
    }

    private static bool IsLarge(XElement img)
    {
        static int Px(string? v) => int.TryParse(v?.Trim().TrimEnd('x', 'p'), out var n) ? n : 0;
        var sizes = new List<int> { Px((string?)img.Attribute("width")), Px((string?)img.Attribute("height")) };
        sizes.AddRange(CssSize.Matches((string?)img.Attribute("style") ?? "").Select(m => int.Parse(m.Groups[2].Value)));
        return sizes.Any(s => s >= LargeImagePx);
    }
}
