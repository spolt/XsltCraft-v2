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
/// <param name="Kind">Render motor hatasıyla yapılamadıysa hatanın türü.</param>
/// <param name="Line">Motor hatasının satırı (bilinmiyorsa 0).</param>
/// <param name="Failure">Render yapılamadıysa nedeni.</param>
internal sealed record RenderVerification(bool Rendered, bool EquivalentAfterStrip,
    IReadOnlyDictionary<string, int> Occurrences, string? Error, bool Operational = false,
    XsltFailureKind? Kind = null, int Line = 0, RenderFailure Failure = RenderFailure.None);

internal enum RenderFailure
{
    None,
    /// <summary>Motor hata döndü (<see cref="RenderVerification.Kind"/>).</summary>
    Engine,
    /// <summary>İşaretli ve işaretsiz sürüm farklı motorlarla çalıştı.</summary>
    EngineMismatch,
    /// <summary>Çıktı karşılaştırma sınırını aştı.</summary>
    OutputLimit,
}

/// <summary>
/// İşaretli temanın örnek belgeyle render edilip doğrulanması — upload sırasında çalışan değişmez:
/// işaretler çıkınca HTML işaretsiz sürümle aynı olmalı ve her yuva çıktıda beklenen sayıda geçmeli.
/// İki render temanın motoruyla (.NET ya da Saxon) yapılır; motorlar farklıysa değişmez doğrulanamaz → başarısız.
/// </summary>
internal sealed class ThemeRenderVerifier(IXsltExecutionService xslt, IThemeSampleXmlProvider samples)
{
    /// <summary>Karşılaştırılan her render çıktısının üst sınırı (karakter).</summary>
    public const int MaxComparedChars = 4 * 1024 * 1024;

    public async Task<RenderVerification> VerifyAsync(string baseXslt, string markedXslt, DocumentType type,
        IEnumerable<string> slotIds, XsltRoute route, Guid userId, CancellationToken cancellationToken)
    {
        var xml = samples.Get(type);
        var baseRun = await xslt.RenderAsync(route, baseXslt, xml, userId, cancellationToken).ConfigureAwait(false);
        if (baseRun.Diagnostic is { } baseError)
            return Failed(baseError);
        var markedRun = await xslt.RenderAsync(route, markedXslt, xml, userId, cancellationToken).ConfigureAwait(false);
        if (markedRun.Diagnostic is { } markedError)
            return Failed(markedError);
        if (baseRun.Engine != markedRun.Engine)
            return new RenderVerification(false, false, new Dictionary<string, int>(),
                $"İşaretli ve işaretsiz tema farklı XSLT motorlarıyla çalıştı ({baseRun.Engine} / {markedRun.Engine}).",
                Failure: RenderFailure.EngineMismatch);

        var baseHtml = baseRun.Html ?? "";
        var markedHtml = markedRun.Html ?? "";
        if (baseHtml.Length > MaxComparedChars || markedHtml.Length > MaxComparedChars)
            return OverLimit();

        try
        {
            var equivalent = HtmlComparison.Normalize(ThemeSlotMarkers.StripFromHtml(markedHtml)) == HtmlComparison.Normalize(baseHtml);
            var occurrences = slotIds.Distinct().ToDictionary(id => id, id => ThemeSlotMarkers.CountOccurrences(markedHtml, id));
            return new RenderVerification(true, equivalent, occurrences, null);
        }
        catch (RegexMatchTimeoutException)
        {
            return OverLimit();
        }
    }

    private static RenderVerification Failed(XsltDiagnostic diagnostic) =>
        new(false, false, new Dictionary<string, int>(), ThemeXsltRuns.Describe(diagnostic), ThemeXsltRuns.IsOperational(diagnostic),
            diagnostic.Kind, diagnostic.Line, RenderFailure.Engine);

    private static RenderVerification OverLimit() =>
        new(false, false, new Dictionary<string, int>(), "Render çıktısı karşılaştırma sınırını aştı.", Failure: RenderFailure.OutputLimit);
}
