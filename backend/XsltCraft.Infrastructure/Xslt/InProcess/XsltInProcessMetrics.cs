using System.Diagnostics.Metrics;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Süreç içi grid ölçümleri — <see cref="XsltWorkerMetrics.MeterName"/> meter'ına eklenir:
/// <list type="bullet">
/// <item><c>xslt.requests{place=inprocess, op, outcome}</c> + <c>xslt.request.duration</c> (havuzla ortak araç).</item>
/// <item><c>xslt.inproc.runaway</c> (gauge): çağıranın bıraktığı, hâlâ çalışan iş thread'leri.</item>
/// <item><c>xslt.inproc.active</c> (gauge): çalışan iş thread'leri (kaçaklar dahil).</item>
/// <item><c>xslt.inproc.degraded</c> (gauge 0/1): bozulmuş mod (kaçak ≥ RunawayMax).</item>
/// <item><c>xslt.inproc.degraded.routed{outcome=worker|unavailable}</c> (sayaç): bozulmuş modda yönlendirilen istekler.</item>
/// </list>
/// Hash/kullanıcı ETİKETLENMEZ (yalnız log).
/// </summary>
public sealed class XsltInProcessMetrics
{
    private readonly XsltWorkerMetrics _shared;
    private readonly Counter<long> _degradedRouted;
    private XsltExecutionGuard? _guard;

    public XsltInProcessMetrics(XsltWorkerMetrics shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        _shared = shared;
        var meter = shared.Meter;
        meter.CreateObservableGauge("xslt.inproc.runaway", () => _guard?.RunawayCount ?? 0,
            description: "Süre aşımı/iptal sonrası hâlâ çalışan süreç içi XSLT thread'leri.");
        meter.CreateObservableGauge("xslt.inproc.active", () => _guard?.ActiveCount ?? 0,
            description: "Çalışan süreç içi XSLT thread'leri (kaçaklar dahil).");
        meter.CreateObservableGauge("xslt.inproc.degraded", () => _guard?.IsDegraded == true ? 1 : 0,
            description: "Bozulmuş mod (1: grid istekleri worker'a yönleniyor).");
        _degradedRouted = meter.CreateCounter<long>("xslt.inproc.degraded.routed",
            description: "Bozulmuş modda yönlendirilen grid istekleri (outcome=worker|unavailable).");
    }

    /// <summary>Gauge kaynağı (DI fabrikası guard'ı kurunca bağlar; döngüsel bağımlılık yok).</summary>
    internal void Attach(XsltExecutionGuard guard) => _guard = guard;

    internal void Request(string op, string outcome, double durationMs) =>
        _shared.Request(op, outcome, durationMs, XsltWorkerMetrics.PlaceInProcess);

    internal void DegradedRouted(string outcome) =>
        _degradedRouted.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
