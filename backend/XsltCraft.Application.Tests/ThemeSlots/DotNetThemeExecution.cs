using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>
/// Tema hattı için sahte <see cref="IXsltExecutionService"/>: gerçek servisle aynı motor seçimi
/// (<see cref="XsltEngineSelector.SelectForTheme"/>, <c>Auto</c>), .NET temalar gerçek <see cref="DotNetXsltEngine"/>'le
/// (süreç içi host'la aynı motor — çıktı <c>XsltHtmlRenderer</c> ile bit-aynı) eşzamanlı çalışır. Bu proje Saxon'u
/// göremez: Saxon seçilen tema <see cref="XsltFailureKind.EngineUnavailable"/> döner (gerçek uçtan uca testler
/// XsltCraft.XsltWorker.Tests/Themes).
/// </summary>
internal sealed class DotNetThemeExecution : IXsltExecutionService
{
    private static readonly DotNetXsltEngine Engine = new();

    public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default)
    {
        var engine = XsltEngineSelector.SelectForTheme(XsltEngineMode.Auto, XsltVersionSniffer.Sniff(request.Xslt), request.Xslt);
        if (engine != XsltEngineKind.DotNet)
            return Task.FromResult(Failed(new XsltDiagnostic("Saxon bu test projesinde yok.", 0, 0,
                XsltFailureKind.EngineUnavailable, engine, "WorkerDisabled"), engine));

        try
        {
            var compiled = Engine.Compile(request.Xslt, XsltExecutionLimits.Default);
            if (request.Op == XsltOperation.Validate)
                return Task.FromResult(new XsltRunResult(null, engine, null, false, null));

            using var output = new StringWriter();
            var timings = compiled.Transform(request.Xml ?? "", output, XsltExecutionLimits.Default);
            return Task.FromResult(new XsltRunResult(output.ToString(), engine, timings, false, null));
        }
        catch (XsltProcessingException ex)
        {
            return Task.FromResult(Failed(ex.Diagnostic, engine));
        }
    }

    private static XsltRunResult Failed(XsltDiagnostic diagnostic, XsltEngineKind engine) => new(null, engine, null, false, diagnostic);
}

/// <summary>
/// Eşzamanlı test kısayolları: <see cref="DotNetThemeExecution"/> eşzamanlı tamamlandığı için async tema servisleri
/// de tamamlanmış görev döner (bekleme/kilitlenme yok).
/// </summary>
internal static class ThemeServiceTestExtensions
{
    public static ThemeSlotAnalysis Analyze(this IThemeSlotAnalyzer analyzer, string xslt, DocumentType type,
        IReadOnlyCollection<string> occupiedHolderIds, IReadOnlyList<ThemeImageDecision>? imageRoles = null) =>
        Completed(analyzer.AnalyzeAsync(xslt, type, occupiedHolderIds, imageRoles));

    public static ThemeUploadOutcome Process(this IThemeUploadProcessor processor, string xslt, DocumentType type,
        OverridesDecision? decision, IReadOnlyList<ThemeImageDecision>? imageRoles = null) =>
        Completed(processor.ProcessAsync(xslt, type, decision, imageRoles));

    private static T Completed<T>(Task<T> task)
    {
        Assert.True(task.IsCompleted, "Sahte tema yürütmesi eşzamanlı tamamlanmalı.");
        return task.GetAwaiter().GetResult();
    }
}
