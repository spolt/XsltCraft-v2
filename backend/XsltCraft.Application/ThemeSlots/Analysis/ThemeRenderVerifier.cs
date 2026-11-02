using System.Text.RegularExpressions;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
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
/// <param name="Operational">Render temadan bağımsız (geçici) bir motor durumu yüzünden yapılamadı.</param>
internal sealed record RenderVerification(bool Rendered, bool EquivalentAfterStrip,
    IReadOnlyDictionary<string, int> Occurrences, string? Error, bool Operational = false);

/// <summary>
/// İşaretli temanın örnek belgeyle render edilip doğrulanması — upload sırasında çalışan değişmez:
/// işaretler çıkınca HTML işaretsiz sürümle aynı olmalı ve her yuva çıktıda beklenen sayıda geçmeli.
/// İki render temanın motoruyla (.NET ya da Saxon) yapılır; motorlar farklıysa değişmez doğrulanamaz → başarısız.
/// </summary>
internal sealed class ThemeRenderVerifier(IXsltExecutionService xslt, IThemeSampleXmlProvider samples)
{
    private static readonly Regex AroundTags = new(@"\s*(<[^>]*>)\s*", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    public async Task<RenderVerification> VerifyAsync(string baseXslt, string markedXslt, DocumentType type,
        IEnumerable<string> slotIds, Guid userId, CancellationToken cancellationToken)
    {
        var xml = samples.Get(type);
        var baseRun = await xslt.RenderThemeAsync(baseXslt, xml, userId, cancellationToken).ConfigureAwait(false);
        if (baseRun.Diagnostic is { } baseError)
            return Failed(baseError);
        var markedRun = await xslt.RenderThemeAsync(markedXslt, xml, userId, cancellationToken).ConfigureAwait(false);
        if (markedRun.Diagnostic is { } markedError)
            return Failed(markedError);
        if (baseRun.Engine != markedRun.Engine)
            return new RenderVerification(false, false, new Dictionary<string, int>(),
                $"İşaretli ve işaretsiz tema farklı XSLT motorlarıyla çalıştı ({baseRun.Engine} / {markedRun.Engine}).");

        var baseHtml = baseRun.Html ?? "";
        var markedHtml = markedRun.Html ?? "";
        var equivalent = Normalize(ThemeSlotMarkers.StripFromHtml(markedHtml)) == Normalize(baseHtml);
        var occurrences = slotIds.Distinct().ToDictionary(id => id, id => ThemeSlotMarkers.CountOccurrences(markedHtml, id));
        return new RenderVerification(true, equivalent, occurrences, null);
    }

    private static RenderVerification Failed(XsltDiagnostic diagnostic) =>
        new(false, false, new Dictionary<string, int>(), ThemeXsltRuns.Describe(diagnostic), ThemeXsltRuns.IsOperational(diagnostic));

    // Girintili çıktı (html yönteminin varsayılanı) işaretin çevresine satır sonu ekler; karşılaştırma etiket
    // çevresindeki boşluktan bağımsızdır.
    private static string Normalize(string html) => Spaces.Replace(AroundTags.Replace(html, "$1"), " ").Trim();
}
