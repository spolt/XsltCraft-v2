using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Api süreci içi korumalı XSLT host'u (grid; worker kapalıyken 1.0 Raw/UserTemplate da; ADR 008 §7).
/// <list type="bullet">
/// <item><b>Koruma:</b> derleme + dönüşüm <see cref="XsltExecutionGuard"/> thread'inde (16 MB yığın, slot semaforu,
///   sert süre = <see cref="XsltExecutionLimits.Deadline"/>). Süre aşımı → <see cref="XsltFailureKind.Timeout"/>,
///   slot yok → <see cref="XsltFailureKind.Busy"/>.</item>
/// <item><b>Sınırlar</b> çağıranın <see cref="XsltExecutionLimits"/>'inden (grid: derinlik 64, 200k düğüm, 8 s;
///   çıktı 8M — .NET <c>LimitedTextWriter</c> / Saxon <c>LimitedJavaWriter</c>). Motorlara giden
///   <see cref="XsltExecutionLimits.Cancellation"/> guard belirtecidir: çağıran bırakınca (süre/iptal) iptal olur.</item>
/// <item><b>Önbellek:</b> en çok 32 giriş / 64M stylesheet karakteri, anahtar <c>(motor, SHA-256(xslt), derinlik)</c>,
///   yalnız başarılı derleme.</item>
/// <item><b>Bozulmuş mod:</b> kaçak ≥ <see cref="XsltGridOptions.RunawayMax"/> iken yeni istekler
///   <see cref="IXsltIsolatedHost"/>'a gider (sonuç <see cref="XsltHostResult.Place"/> = Isolated — kapı fiilî yere
///   göre sayar); worker kapalıysa <see cref="XsltFailureKind.EngineUnavailable"/>. Kaçaklar bitince süreç içine döner.</item>
/// </list>
/// Loglar: xsltHash (ilk 16 hex), kullanıcı, süre — içerik YOK.
/// </summary>
public sealed class InProcessXsltHost : IXsltInProcessHost, IDisposable
{
    public const int DefaultCacheEntries = 32;

    internal const string BusyCode = "InProcessBusy";
    internal const string TimeoutCode = "InProcessTimeout";
    internal const string DegradedCode = "InProcessDegraded";
    internal const string StackCode = "InProcessStack";
    internal const string CooperativeTimeoutCode = "InProcessCooperativeDeadline";

    /// <summary>
    /// Motor (işbirlikçi) süresi sert guard süresinden bu kadar kısadır: normal sınır aşımı motor içinde temiz
    /// <see cref="XsltFailureKind.Timeout"/> olur, kaçak üretmez (worker <c>CooperativeMargin</c> ile aynı mantık).
    /// </summary>
    internal static readonly TimeSpan CooperativeMargin = TimeSpan.FromMilliseconds(500);

    private readonly XsltExecutionGuard _guard;
    private readonly InProcessXsltEngines _engines;
    private readonly IXsltIsolatedHost _isolated;
    private readonly XsltInProcessMetrics? _metrics;
    private readonly ILogger<InProcessXsltHost> _logger;
    private readonly InProcessCompileCache _cache;

    public InProcessXsltHost(
        XsltExecutionGuard guard,
        InProcessXsltEngines engines,
        IXsltIsolatedHost isolated,
        ILogger<InProcessXsltHost> logger,
        XsltInProcessMetrics? metrics = null,
        int cacheEntries = DefaultCacheEntries)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(isolated);
        ArgumentNullException.ThrowIfNull(logger);
        _guard = guard;
        _engines = engines;
        _isolated = isolated;
        _logger = logger;
        _metrics = metrics;
        _cache = new InProcessCompileCache(cacheEntries);
    }

    /// <summary>Önbellekteki derlenmiş stylesheet sayısı (tanı/test).</summary>
    internal int CachedCount => _cache.Count;

    public async Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.Xslt)));
        var ticket = new XsltGuardTicket(sha256[..16], request.UserId, request.Engine);

        if (_guard.IsDegraded)
            return await RunDegradedAsync(request, ticket, cancellationToken).ConfigureAwait(false);

        var started = Stopwatch.GetTimestamp();
        using var linked = LinkIfDistinct(request.Limits.Cancellation, cancellationToken);
        var waitToken = linked?.Token ?? (cancellationToken.CanBeCanceled ? cancellationToken : request.Limits.Cancellation);
        XsltHostResult result;
        try
        {
            var outcome = await _guard.RunAsync(ticket, request.Limits.Deadline,
                token => Execute(request, sha256, token), waitToken).ConfigureAwait(false);

            result = outcome.Status switch
            {
                XsltGuardStatus.Completed => outcome.Value!,
                XsltGuardStatus.Busy => Fail(request.Engine, XsltFailureKind.Busy, BusyCode,
                    "Önizleme motoru şu anda yoğun; lütfen birazdan tekrar deneyin."),
                _ => Fail(request.Engine, XsltFailureKind.Timeout, TimeoutCode, "XSLT dönüşümü süre sınırını aştı.")
                    with { Runaway = outcome.Runaway },
            };
        }
        catch (OperationCanceledException ex)
        {
            // XsltRunawayCanceledException (iş kaçağa döndü) olduğu gibi yükselir: servis kesiciye Timeout bildirir.
            Record(request, ex is XsltRunawayCanceledException ? "canceled-runaway" : "canceled", started);
            throw;
        }

        Record(request, result.Diagnostic is null ? "ok" : result.Diagnostic.Kind.ToString(), started);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Süreç içi XSLT: op={Op} motor={Engine} sonuç={Outcome} önbellek={CacheHit} xsltHash={XsltHash} kullanıcı={UserId} süre={DurationMs:F0}ms",
                request.Op, result.Engine, result.Diagnostic?.Kind.ToString() ?? "ok", result.CacheHit, ticket.XsltHash, ticket.UserId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return result with { Place = XsltExecutionPlace.InProcess };
    }

    public void Dispose() => _cache.Dispose();

    // ------------------------------------------------------------------ bozulmuş mod

    private async Task<XsltHostResult> RunDegradedAsync(XsltHostRequest request, XsltGuardTicket ticket, CancellationToken cancellationToken)
    {
        if (!_isolated.IsEnabled)
        {
            _metrics?.DegradedRouted("unavailable");
            _logger.LogWarning("Süreç içi XSLT bozulmuş modda ve worker kapalı → EngineUnavailable: xsltHash={XsltHash} kullanıcı={UserId} kaçak={Runaways}",
                ticket.XsltHash, ticket.UserId, _guard.RunawayCount);
            return Fail(request.Engine, XsltFailureKind.EngineUnavailable, DegradedCode,
                "Önizleme motoru geçici olarak kullanılamıyor; lütfen birazdan tekrar deneyin.");
        }

        _metrics?.DegradedRouted("worker");
        _logger.LogInformation("Süreç içi XSLT bozulmuş modda → worker: xsltHash={XsltHash} kullanıcı={UserId} kaçak={Runaways}",
            ticket.XsltHash, ticket.UserId, _guard.RunawayCount);

        var result = await _isolated.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result with { Place = XsltExecutionPlace.Isolated };
    }

    // ------------------------------------------------------------------ guard thread'i

    /// <summary>Guard thread'inde çalışır: önbellek/derleme → (Render ise) dönüşüm. İçerik hataları teşhis olarak döner.</summary>
    private XsltHostResult Execute(XsltHostRequest request, string sha256, CancellationToken abandon)
    {
        var cooperative = CooperativeDeadline(request.Limits.Deadline);
        var limits = request.Limits with { Deadline = cooperative, Cancellation = abandon };
        var depth = limits.MaxStylesheetDepth;
        try
        {
            var sw = Stopwatch.StartNew();
            var cacheHit = _cache.TryGet(request.Engine, sha256, depth, out var compiled);
            if (!cacheHit)
            {
                compiled = _engines.Get(request.Engine).Compile(request.Xslt, limits);
                _cache.Add(request.Engine, sha256, depth, request.Xslt.Length, compiled);
            }

            var compileMs = cacheHit ? 0 : sw.ElapsedMilliseconds;
            if (request.Op == XsltOperation.Validate)
                return new XsltHostResult(null, request.Engine, new XsltTransformTimings(0, compileMs, 0, 0), cacheHit, null);

            // Dönüşüm kalan işbirlikçi süreyle (derleme + dönüşüm toplamı sert sürenin altında kalsın).
            var remaining = cooperative > TimeSpan.Zero ? cooperative - sw.Elapsed : cooperative;
            if (cooperative > TimeSpan.Zero && remaining <= TimeSpan.Zero)
                return Fail(request.Engine, XsltFailureKind.Timeout, CooperativeTimeoutCode, "XSLT dönüşümü süre sınırını aştı.");

            using var output = new StringWriter();
            var timings = compiled.Transform(request.Xml ?? "", output, limits with { Deadline = remaining }) with { CompileMs = compileMs };
            return new XsltHostResult(output.ToString(), request.Engine, timings, cacheHit, null);
        }
        catch (XsltProcessingException ex)
        {
            return XsltHostResult.Failed(ex.Diagnostic, request.Engine);
        }
        catch (InsufficientExecutionStackException)
        {
            return Fail(request.Engine, XsltFailureKind.LimitExceeded, StackCode, "XSLT işlemesi çok derin iç içe geçti (yığın sınırı).");
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    /// <summary>Sert süre − pay; pay süreyi tüketiyorsa (çok kısa sınır) sert süre; ≤ 0 (sınırsız) olduğu gibi.</summary>
    internal static TimeSpan CooperativeDeadline(TimeSpan hard)
    {
        if (hard <= TimeSpan.Zero)
            return hard;
        var cooperative = hard - CooperativeMargin;
        return cooperative > TimeSpan.Zero ? cooperative : hard;
    }

    /// <summary>
    /// Beklemeyi hem host belirteci hem istek sınırlarındaki iptal keser; ikisi farklı ve iptal edilebilirse bağlı
    /// kaynak (çağıran dispose eder), aksi hâlde <c>null</c>. Guard'ın iş belirteci bundan bağımsızdır.
    /// </summary>
    private static CancellationTokenSource? LinkIfDistinct(CancellationToken limitsToken, CancellationToken hostToken) =>
        limitsToken.CanBeCanceled && hostToken.CanBeCanceled && limitsToken != hostToken
            ? CancellationTokenSource.CreateLinkedTokenSource(hostToken, limitsToken)
            : null;

    private void Record(XsltHostRequest request, string outcome, long started) =>
        _metrics?.Request(request.Op == XsltOperation.Render ? "render" : "validate", outcome,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private static XsltHostResult Fail(XsltEngineKind engine, XsltFailureKind kind, string code, string message) =>
        XsltHostResult.Failed(new XsltDiagnostic(message, 0, 0, kind, engine, code), engine);
}
