using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <param name="Detected">Temadaki statik görseller (<see cref="IThemeSlotAnalyzer.DetectImages"/> ile aynı liste).</param>
/// <param name="Roles"><see cref="Detected"/> ile hizalı önceki roller; işareti olmayan görsel null.</param>
/// <param name="Dynamic"><see cref="Detected"/> ile hizalı: görselin bir niteliği değer şablonu taşıyor.</param>
internal sealed record PriorImages(IReadOnlyList<ThemeImageInfo> Detected, IReadOnlyList<ThemeImageRole?> Roles,
    IReadOnlyList<bool> Dynamic);

/// <summary>
/// Önceki analizin işaretlerinden (<c>data-xc-placeholder="logo|signature"</c>) temadaki görsellerin rollerini geri
/// okur. Liste <see cref="IThemeSlotAnalyzer.DetectImages"/> sırasıyla hizalıdır; işareti olmayan görsel null.
/// Eşleme gövdedeki literal <c>img</c>'lerin belge sırasıyladır (satır numarası işaretlerle kayabilir).
/// </summary>
internal static class PriorImageRoleReader
{
    public static PriorImages Read(string xsltWithMarkers, DocumentType type)
    {
        if (Align(xsltWithMarkers, type) is not { } aligned)
            return new PriorImages([], [], []);

        var infos = aligned.Detected.Select(i => i.ToInfo()).ToList();
        var dynamic = aligned.Detected.Select(i => StaticImageSources.HasDynamicAttribute(i.Element)).ToList();
        return aligned.Elements is { } elements
            ? new PriorImages(infos, elements.Select(RoleOf).ToList(), dynamic)
            : new PriorImages(infos, aligned.Detected.Select(_ => (ThemeImageRole?)null).ToList(), dynamic);
    }

    /// <summary>İşaretli metindeki görsel elemanları, <see cref="IThemeSlotAnalyzer.DetectImages"/> sırasıyla; metin
    /// ayrıştırılamaz ya da işaretli/işaretsiz görsel sayısı tutmazsa null.</summary>
    public static (ThemeDocument Marked, IReadOnlyList<XElement> Elements)? Locate(string xsltWithMarkers, DocumentType type) =>
        Align(xsltWithMarkers, type) is { Marked: { } marked, Elements: { } elements } ? (marked, elements) : null;

    private sealed record Alignment(List<DetectedImage> Detected, ThemeDocument? Marked, List<XElement>? Elements);

    private static Alignment? Align(string xsltWithMarkers, DocumentType type)
    {
        if (!TryDocument(ThemeSlotMarkers.Clean(xsltWithMarkers), type, out var clean))
            return null;

        var detected = ThemeImageDetector.Detect(clean, new ThemeAnchorDetector().Detect(clean), new InsertionPointResolver(clean));
        var cleanImages = BodyImages(clean);
        if (!TryDocument(xsltWithMarkers, type, out var marked) || BodyImages(marked) is not { } markedImages
            || cleanImages.Count != markedImages.Count)
            return new Alignment(detected, null, null);

        var position = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < cleanImages.Count; i++)
            position[cleanImages[i]] = i;
        return new Alignment(detected, marked, detected.Select(i => markedImages[position[i.Element]]).ToList());
    }

    private static bool TryDocument(string xslt, DocumentType type, out ThemeDocument doc)
    {
        doc = null!;
        if (!XsltSourceMap.TryBuild(xslt, out var map, out _) || ThemeDocument.TryCreate(map, type) is not { } d)
            return false;
        doc = d;
        return true;
    }

    private static List<XElement> BodyImages(ThemeDocument d) =>
        d.Body.Descendants().Where(e => IsLiteral(e, "img")).ToList();

    private static ThemeImageRole? RoleOf(XElement img) => (string?)img.Attribute(ThemeSlotMarkers.PlaceholderAttribute) switch
    {
        "logo" => ThemeImageRole.Logo,
        "signature" => ThemeImageRole.Signature,
        _ => null,
    };
}
