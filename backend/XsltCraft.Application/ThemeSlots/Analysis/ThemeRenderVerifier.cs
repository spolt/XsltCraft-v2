using System.Text.RegularExpressions;

using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>Analiz doğrulaması için gömülü örnek belge (frontend varsayılan XML'lerinin kopyası).</summary>
public interface IThemeSampleXmlProvider
{
    string Get(DocumentType type);
}

public sealed class ThemeSampleXmlProvider : IThemeSampleXmlProvider
{
    private static readonly Lazy<string> Invoice = new(() => Load("invoice.xml"));
    private static readonly Lazy<string> Despatch = new(() => Load("despatch.xml"));

    public string Get(DocumentType type) => type == DocumentType.Despatch ? Despatch.Value : Invoice.Value;

    private static string Load(string name)
    {
        var asm = typeof(ThemeSampleXmlProvider).Assembly;
        var resource = asm.GetManifestResourceNames().Single(n => n.EndsWith($".ThemeSlots.Samples.{name}", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <param name="Rendered">İki sürüm de örnek belgeyle render edilebildi.</param>
/// <param name="EquivalentAfterStrip">İşaretler çıkarıldığında çıktı işaretsiz sürümle aynı (yerleşim bozulmadı).</param>
/// <param name="Occurrences">Her yuvanın çıktıdaki geçiş sayısı.</param>
internal sealed record RenderVerification(bool Rendered, bool EquivalentAfterStrip,
    IReadOnlyDictionary<string, int> Occurrences, string? Error);

/// <summary>
/// İşaretli temanın örnek belgeyle render edilip doğrulanması — upload sırasında çalışan değişmez:
/// işaretler çıkınca HTML işaretsiz sürümle aynı olmalı ve her yuva çıktıda beklenen sayıda geçmeli.
/// </summary>
internal sealed class ThemeRenderVerifier(IXsltHtmlRenderer renderer, IThemeSampleXmlProvider samples)
{
    private static readonly Regex AroundTags = new(@"\s*(<[^>]*>)\s*", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    public RenderVerification Verify(string baseXslt, string markedXslt, DocumentType type, IEnumerable<string> slotIds)
    {
        string baseHtml, markedHtml;
        try
        {
            var xml = samples.Get(type);
            baseHtml = renderer.Render(baseXslt, xml).Html;
            markedHtml = renderer.Render(markedXslt, xml).Html;
        }
        catch (Exception ex)
        {
            return new RenderVerification(false, false, new Dictionary<string, int>(), ex.Message);
        }

        var equivalent = Normalize(ThemeSlotMarkers.StripFromHtml(markedHtml)) == Normalize(baseHtml);
        var occurrences = slotIds.Distinct().ToDictionary(id => id, id => ThemeSlotMarkers.CountOccurrences(markedHtml, id));
        return new RenderVerification(true, equivalent, occurrences, null);
    }

    // Girintili çıktı (html yönteminin varsayılanı) işaretin çevresine satır sonu ekler; karşılaştırma etiket
    // çevresindeki boşluktan bağımsızdır.
    private static string Normalize(string html) => Spaces.Replace(AroundTags.Replace(html, "$1"), " ").Trim();
}
