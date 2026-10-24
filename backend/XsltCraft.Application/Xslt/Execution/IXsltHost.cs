using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// Host'a verilen, tamamen çözülmüş istek: motor seçilmiş, boyut/güvenlik denetimi yapılmış.
/// <see cref="XsltExecutionLimits.Deadline"/> host'un sert süre sınırıdır.
/// </summary>
public sealed record XsltHostRequest(
    XsltOperation Op,
    XsltEngineKind Engine,
    string Xslt,
    string? Xml,
    XsltExecutionLimits Limits)
{
    /// <summary>Yalnız log/tanı için (içerik loglanmaz); kapı kararı servistedir.</summary>
    public Guid UserId { get; init; }
}

/// <summary>Host yanıtı. <see cref="Diagnostic"/> <c>null</c> → başarılı.</summary>
public sealed record XsltHostResult(
    string? Html,
    XsltEngineKind Engine,
    XsltTransformTimings? Timings,
    bool CacheHit,
    XsltDiagnostic? Diagnostic)
{
    /// <summary>
    /// İsteğin FİİLEN çalıştığı yer; <c>null</c> → çağıranın seçtiği yer. Süreç içi host bozulmuş modda
    /// worker'a yönlendirdiğinde <see cref="XsltExecutionPlace.Isolated"/> bildirir — kullanıcı kapısının
    /// kötüye kullanım kesicisi bu yere göre sayar.
    /// </summary>
    public XsltExecutionPlace? Place { get; init; }

    public static XsltHostResult Failed(XsltDiagnostic diagnostic, XsltEngineKind engine) =>
        new(null, engine, null, false, diagnostic);
}

/// <summary>
/// XSLT çalıştırma portu. İçerik hataları ve sınır ihlalleri <see cref="XsltHostResult.Diagnostic"/>
/// olarak döner; yalnız iptal (<see cref="OperationCanceledException"/>) ve programlama hataları fırlar.
/// </summary>
public interface IXsltHost
{
    Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// İzole worker süreci (ADR 008). Kullanıcının yazdığı XSLT (Raw, Validate, UserTemplate,
/// script'li grid) yalnız buradan çalışır. Uygulama: <c>Infrastructure WorkerXsltHost</c>.
/// </summary>
public interface IXsltIsolatedHost : IXsltHost
{
    /// <summary>
    /// Worker yapılandırmada açık mı (<c>XsltWorker:Enabled</c>). <c>false</c> → servis 1.0
    /// stylesheet'leri süreç içi .NET'e düşürür, ≥ 2.0 → <see cref="XsltFailureKind.EngineUnavailable"/>.
    /// Anlık hazır olma durumu DEĞİLDİR (hazır worker yoksa host <c>EngineUnavailable</c> döner).
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Api süreci içi korumalı yürütme (grid; worker kapalıyken 1.0 Raw/UserTemplate da; ADR 008 §7).
/// Uygulama: <c>Infrastructure InProcessXsltHost</c> (<see cref="XsltExecutionGuard"/>, bozulmuş mod → worker, Saxon).
/// </summary>
public interface IXsltInProcessHost : IXsltHost;
