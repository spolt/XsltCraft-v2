using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Application.ThemeSlots;

/// <summary>
/// Tema hattının tek yürütme yolu (T-izi): <see cref="XsltRoute.Theme"/> üzerinden <see cref="IXsltExecutionService"/>.
/// Motor sürüm + 2.0 yapısıyla seçilir (<see cref="XsltEngineSelector.SelectForTheme"/>): 1.0 temalar .NET'te süreç içi
/// (çıktı eskisiyle bit-aynı), 2.0 temalar Saxon'da izole worker'da. Aynı stylesheet hep aynı motora gider —
/// analizdeki işaretsiz/işaretli karşılaştırması tek motorla yapılır.
/// </summary>
internal static class ThemeXsltRuns
{
    public static Task<XsltRunResult> RenderThemeAsync(this IXsltExecutionService xslt, string stylesheet, string xml,
        Guid userId, CancellationToken cancellationToken) =>
        xslt.ExecuteAsync(new XsltRunRequest(XsltRoute.Theme, stylesheet, xml, XsltOperation.Render, userId), cancellationToken);

    /// <summary>Yalnız derleme (temanın seçilen motoruyla).</summary>
    public static Task<XsltRunResult> CompileThemeAsync(this IXsltExecutionService xslt, string stylesheet,
        Guid userId, CancellationToken cancellationToken) =>
        xslt.ExecuteAsync(new XsltRunRequest(XsltRoute.Theme, stylesheet, null, XsltOperation.Validate, userId), cancellationToken);

    /// <summary>İçerikten bağımsız (geçici) motor durumu: tema hatalı sayılmaz, işlem yeniden denenmeli.</summary>
    public static bool IsOperational(XsltDiagnostic diagnostic) =>
        diagnostic.Kind is XsltFailureKind.Busy or XsltFailureKind.UserBusy or XsltFailureKind.EngineUnavailable
            or XsltFailureKind.EngineCrashed or XsltFailureKind.EngineInternal;

    public static string Describe(XsltDiagnostic diagnostic) =>
        diagnostic.Line > 0 ? $"{diagnostic.Message} (satır {diagnostic.Line})" : diagnostic.Message;
}
