using System.Globalization;
using System.Text;
using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// Rota yönlendirici (ADR 007 §2, ADR 008). Akış:
/// <list type="number">
/// <item>Boyut: karakter + UTF-8 bayt (worker çerçeve bütçesi) → <see cref="XsltFailureKind.LimitExceeded"/>.</item>
/// <item><see cref="IXsltSafetyScanner"/> <see cref="XsltSafetyMode.Execute"/> → <c>Security</c>/<c>Unsupported</c> (satır/kolon).</item>
/// <item><see cref="XsltVersionSniffer"/> (&gt; 2.0 → <c>Unsupported</c>) + <see cref="XsltEngineSelector"/> (rota modu).</item>
/// <item>Yer: Raw/Validate/UserTemplate ve script'li grid → izole; grid → süreç içi.
///   Worker kapalıysa 1.0 → süreç içi .NET, ≥ 2.0 ve script'li grid → <c>EngineUnavailable</c>.</item>
/// <item><see cref="IXsltUserGate"/> → host → sonuç kapıya FİİLÎ yerle (<see cref="XsltHostResult.Place"/>)
///   bildirilir, izin her durumda bırakılır.</item>
/// </list>
/// </summary>
public sealed class XsltExecutionService : IXsltExecutionService
{
    private const decimal MaxSupportedVersion = 2.0m;

    private readonly XsltExecutionOptions _options;
    private readonly IXsltSafetyScanner _scanner;
    private readonly IXsltUserGate _gate;
    private readonly IXsltIsolatedHost _isolatedHost;
    private readonly IXsltInProcessHost _inProcessHost;

    public XsltExecutionService(
        XsltExecutionOptions options,
        IXsltSafetyScanner scanner,
        IXsltUserGate gate,
        IXsltIsolatedHost isolatedHost,
        IXsltInProcessHost inProcessHost)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = options.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"{XsltExecutionOptions.SectionName} ayarları geçersiz: {string.Join(" ", errors)}", nameof(options));

        _options = options;
        _scanner = scanner;
        _gate = gate;
        _isolatedHost = isolatedHost;
        _inProcessHost = inProcessHost;
    }

    public async Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Xslt);

        var lane = LaneFor(request);
        var routeLimits = RouteLimitsFor(request);
        var xml = request.Op == XsltOperation.Render ? request.Xml ?? "" : null;

        var rejection = CheckSize(request.Xslt, xml, routeLimits) ?? Scan(request.Xslt);
        if (rejection is not null)
            return XsltRunResult.Failed(rejection);

        var version = XsltVersionSniffer.Sniff(request.Xslt);
        if (CheckVersion(version) is { } unsupported)
            return XsltRunResult.Failed(unsupported);

        var engine = XsltEngineSelector.Select(ModeFor(request.Route), version);
        var (place, unavailable) = ResolvePlace(request, engine);
        if (unavailable is not null)
            return XsltRunResult.Failed(unavailable, engine);

        if (!_gate.TryAcquire(request.UserId, lane, place, out var lease, out var denial))
        {
            return XsltRunResult.Failed(new XsltDiagnostic(denial.Message, 0, 0, XsltFailureKind.UserBusy, engine, denial.ErrorCode), engine)
                with { RetryAfter = denial.RetryAfter };
        }

        var held = lease;
        try
        {
            var hostRequest = new XsltHostRequest(request.Op, engine, request.Xslt, xml, BuildLimits(routeLimits, cancellationToken))
            {
                UserId = request.UserId,
            };
            var host = place == XsltExecutionPlace.Isolated ? (IXsltHost)_isolatedHost : _inProcessHost;

            XsltHostResult result;
            try
            {
                result = await host.ExecuteAsync(hostRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (XsltProcessingException ex)
            {
                result = XsltHostResult.Failed(ex.Diagnostic, engine);
            }
            catch (XsltRunawayCanceledException ex)
            {
                // İstemci iptali süreç içi işi kaçağa çevirdi: kesici bunu Timeout sayar (iptal tekrarıyla API bozulmuş
                // modda tutulamaz — G2 Y1) ve izin kaçak bitene dek tutulur.
                lease.ReportOutcome(XsltFailureKind.Timeout, XsltExecutionPlace.InProcess);
                held = HoldUntil(lease, ex.RunawayFinished);
                throw;
            }

            // Host fiilî yeri bildirebilir (süreç içi host bozulmuş modda worker'a yönlendirir).
            var actualPlace = result.Place ?? place;
            lease.ReportOutcome(result.Diagnostic?.Kind, actualPlace);
            if (result.Runaway is { } runaway)
                held = HoldUntil(lease, runaway);

            var html = request.Op == XsltOperation.Render && result.Diagnostic is null ? result.Html ?? "" : null;
            return new XsltRunResult(html, result.Engine, result.Timings, result.CacheHit, result.Diagnostic) { Place = actualPlace };
        }
        finally
        {
            held?.Dispose();
        }
    }

    /// <summary>İzni kaçak iş bitince bırakır; çağıranın <c>finally</c>'si artık bırakmaz (<c>null</c> döner).</summary>
    private static IXsltGateLease? HoldUntil(IXsltGateLease lease, Task runaway)
    {
        _ = runaway.ContinueWith(static (_, state) => ((IXsltGateLease)state!).Dispose(), lease,
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return null;
    }

    // ------------------------------------------------------------------ rota çözümü

    private static XsltGateLane LaneFor(XsltRunRequest request)
    {
        EnsureSupportedRoute(request.Route);
        if (request.Op == XsltOperation.Validate)
            return XsltGateLane.Validate;

        return request.Route switch
        {
            XsltRoute.Raw => XsltGateLane.Raw,
            XsltRoute.UserTemplate => XsltGateLane.UserTemplate,
            _ => XsltGateLane.Grid,
        };
    }

    private XsltRouteLimits RouteLimitsFor(XsltRunRequest request)
    {
        if (request.Op == XsltOperation.Validate)
            return _options.Limits.Validate;

        return request.Route switch
        {
            XsltRoute.Raw => _options.Limits.Raw,
            XsltRoute.UserTemplate => _options.Limits.UserTemplate,
            _ => _options.Limits.Grid,
        };
    }

    private XsltEngineMode ModeFor(XsltRoute route) =>
        route == XsltRoute.Raw ? _options.Engine.Raw : _options.Engine.Grid;

    private (XsltExecutionPlace Place, XsltDiagnostic? Unavailable) ResolvePlace(XsltRunRequest request, XsltEngineKind engine)
    {
        var isolated = request.Route != XsltRoute.Grid || request.IsUserAuthoredGrid;
        if (!isolated)
            return (XsltExecutionPlace.InProcess, null);
        if (_isolatedHost.IsEnabled)
            return (XsltExecutionPlace.Isolated, null);

        // Worker kapalı (yerel geliştirme): yalnız kullanıcı kodu içermeyen 1.0 yol süreç içine düşer.
        if (request.Route == XsltRoute.Grid)
            return (XsltExecutionPlace.Isolated, Unavailable(engine, "Script bloklu grid önizlemesi izole XSLT motoru gerektirir; motor bu ortamda devre dışı."));
        if (engine != XsltEngineKind.DotNet)
            return (XsltExecutionPlace.Isolated, Unavailable(engine, "XSLT 2.0 motoru bu ortamda devre dışı (XsltWorker:Enabled=false)."));

        return (XsltExecutionPlace.InProcess, null);
    }

    private static void EnsureSupportedRoute(XsltRoute route)
    {
        if (route is not (XsltRoute.Raw or XsltRoute.Grid or XsltRoute.UserTemplate))
            throw new ArgumentOutOfRangeException(nameof(route), route, "Bu rota XsltExecutionService'ten geçmez (Theme → IXsltHtmlRenderer).");
    }

    private XsltExecutionLimits BuildLimits(XsltRouteLimits routeLimits, CancellationToken cancellationToken) => new()
    {
        Deadline = TimeSpan.FromMilliseconds(routeLimits.TimeoutMs),
        MaxOutputChars = _options.Limits.MaxOutputChars,
        MaxStylesheetDepth = _options.Limits.MaxStylesheetDepth,
        MaxInputDepth = routeLimits.MaxInputDepth,
        MaxInputNodes = routeLimits.MaxInputNodes,
        Cancellation = cancellationToken,
    };

    // ------------------------------------------------------------------ ön denetimler

    private static XsltDiagnostic? CheckSize(string xslt, string? xml, XsltRouteLimits limits) =>
        CheckSize(xslt, limits.MaxXsltChars, limits.MaxXsltBytes, "XSLT", "XsltTooLarge")
        ?? (xml is null ? null : CheckSize(xml, limits.MaxXmlChars, limits.MaxXmlBytes, "XML", "XmlTooLarge"));

    /// <summary>
    /// Önce karakter (O(1)); sonra UTF-8 bayt. Bayt sayımı yalnız en kötü durum (3 bayt/karakter)
    /// sınırı aşabiliyorsa yapılır — küçük girdilerde maliyet yok.
    /// </summary>
    private static XsltDiagnostic? CheckSize(string text, int maxChars, int maxBytes, string label, string code)
    {
        if (text.Length > maxChars)
            return TooLarge($"{label} içeriği çok büyük (en fazla {Group(maxChars)} karakter).", code);

        if ((long)text.Length * 3 > maxBytes && Encoding.UTF8.GetByteCount(text) > maxBytes)
            return TooLarge($"{label} içeriği çok büyük (UTF-8 olarak en fazla {FormatBytes(maxBytes)}).", code);

        return null;
    }

    private XsltDiagnostic? Scan(string xslt)
    {
        var report = _scanner.Scan(xslt, XsltSafetyMode.Execute);
        if (!report.HasFindings)
            return null;

        var finding = report.Findings[0];
        var kind = finding.Severity == XsltSafetySeverity.Security ? XsltFailureKind.Security : XsltFailureKind.Unsupported;
        return new XsltDiagnostic(finding.Message, finding.Line, finding.Column, kind, ErrorCode: finding.Code);
    }

    /// <summary>Tarayıcıdan bağımsız ikinci savunma: XSLT 2.0 tavanı (ADR 007 §4).</summary>
    private static XsltDiagnostic? CheckVersion(decimal? version) =>
        version > MaxSupportedVersion
            ? new XsltDiagnostic(
                $"XSLT sürümü {version.Value.ToString(CultureInfo.InvariantCulture)} desteklenmiyor (tavan 2.0).",
                0, 0, XsltFailureKind.Unsupported, ErrorCode: "VersionAbove20")
            : null;

    private static XsltDiagnostic TooLarge(string message, string code) =>
        new(message, 0, 0, XsltFailureKind.LimitExceeded, ErrorCode: code);

    private static XsltDiagnostic Unavailable(XsltEngineKind engine, string message) =>
        new(message, 0, 0, XsltFailureKind.EngineUnavailable, engine, "WorkerDisabled");

    // Kültürden bağımsız (InvariantGlobalization'da da çalışır) Türkçe basamak gruplama: 2.000.000.
    private static string Group(int value) =>
        value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    private static string FormatBytes(int bytes) =>
        bytes % (1024 * 1024) == 0
            ? $"{bytes / (1024 * 1024)} MB"
            : $"{Group(bytes)} bayt";
}
