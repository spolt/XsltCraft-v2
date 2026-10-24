using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// <see cref="IXsltExecutionService"/> sonucu. <see cref="Diagnostic"/> <c>null</c> ise başarılıdır
/// (<see cref="XsltOperation.Render"/> → <see cref="Html"/> dolu; <see cref="XsltOperation.Validate"/> → <c>null</c>).
/// </summary>
/// <param name="Html">Dönüşüm çıktısı.</param>
/// <param name="Engine">Seçilen/çalışan motor; motor seçiminden önce reddedildiyse <c>null</c>.</param>
/// <param name="Timings">Motor aşama süreleri; motora ulaşılmadıysa <c>null</c>.</param>
/// <param name="CacheHit">Derlenmiş stylesheet önbellekten mi geldi.</param>
/// <param name="Diagnostic">Hata ayrıntısı (<see cref="XsltDiagnostic.Kind"/> → HTTP eşlemesi T3.1).</param>
public sealed record XsltRunResult(
    string? Html,
    XsltEngineKind? Engine,
    XsltTransformTimings? Timings,
    bool CacheHit,
    XsltDiagnostic? Diagnostic)
{
    /// <summary>İsteğin çalıştığı yer; yürütmeden önce reddedildiyse <c>null</c>.</summary>
    public XsltExecutionPlace? Place { get; init; }

    /// <summary><see cref="XsltFailureKind.UserBusy"/> bloğunda kalan süre (<c>Retry-After</c> için).</summary>
    public TimeSpan? RetryAfter { get; init; }

    public bool Succeeded => Diagnostic is null;

    internal static XsltRunResult Failed(XsltDiagnostic diagnostic, XsltEngineKind? engine = null) =>
        new(null, engine, null, false, diagnostic);
}
