using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Application.ThemeSlots;

/// <summary>
/// Tema hattının tek yürütme yolu (T-izi): <see cref="XsltRoute.Theme"/> üzerinden <see cref="IXsltExecutionService"/>.
/// Motor sürüm + 2.0 yapısıyla seçilir (<see cref="XsltEngineSelector.SelectByUsage"/>): 1.0 temalar .NET'te süreç içi
/// (çıktı eskisiyle bit-aynı), 2.0 temalar Saxon'da izole worker'da. Aynı stylesheet hep aynı motora gider —
/// analizdeki işaretsiz/işaretli karşılaştırması tek motorla yapılır.
/// </summary>
internal static class ThemeXsltRuns
{
    /// <param name="route">Yalnız <see cref="XsltRoute.Theme"/> (admin teması) ya da <see cref="XsltRoute.Raw"/>
    /// (kullanıcının yüklediği XSLT — .NET de izole worker'da).</param>
    public static Task<XsltRunResult> RenderAsync(this IXsltExecutionService xslt, XsltRoute route, string stylesheet, string xml,
        Guid userId, CancellationToken cancellationToken) =>
        xslt.ExecuteAsync(new XsltRunRequest(Checked(route), stylesheet, xml, XsltOperation.Render, userId), cancellationToken);

    /// <summary>Yalnız derleme (stylesheet'in seçilen motoruyla).</summary>
    public static Task<XsltRunResult> CompileAsync(this IXsltExecutionService xslt, XsltRoute route, string stylesheet,
        Guid userId, CancellationToken cancellationToken) =>
        xslt.ExecuteAsync(new XsltRunRequest(Checked(route), stylesheet, null, XsltOperation.Validate, userId), cancellationToken);

    private static XsltRoute Checked(XsltRoute route) => route is XsltRoute.Theme or XsltRoute.Raw
        ? route
        : throw new ArgumentOutOfRangeException(nameof(route), route, "Tema hattı yalnız Theme ya da Raw rotasıyla çalışır.");

    /// <summary>İçerikten bağımsız (geçici) motor durumu: tema hatalı sayılmaz, işlem yeniden denenmeli.</summary>
    public static bool IsOperational(XsltDiagnostic diagnostic) =>
        diagnostic.Kind is XsltFailureKind.Busy or XsltFailureKind.UserBusy or XsltFailureKind.EngineUnavailable
            or XsltFailureKind.EngineCrashed or XsltFailureKind.EngineInternal;

    public static string Describe(XsltDiagnostic diagnostic) =>
        diagnostic.Line > 0 ? $"{diagnostic.Message} (satır {diagnostic.Line})" : diagnostic.Message;
}
