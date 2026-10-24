using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// <c>XsltCraft.Xslt</c> meter'ı. Etiketler düşük kardinaliteli (op, outcome, reason); PID/hash/içerik ETİKETLENMEZ.
/// </summary>
public sealed class XsltWorkerMetrics
{
    public const string MeterName = "XsltCraft.Xslt";

    private readonly Counter<long> _started;
    private readonly Counter<long> _exited;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _duration;
    private readonly Histogram<double> _queueWait;
    private Func<int> _readyProvider = static () => 0;

    public XsltWorkerMetrics(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);
        _started = Meter.CreateCounter<long>("xslt.worker.started", description: "Başlatılan worker süreçleri.");
        _exited = Meter.CreateCounter<long>("xslt.worker.exited", description: "Sonlanan worker süreçleri (reason etiketli).");
        _requests = Meter.CreateCounter<long>("xslt.requests", description: "Havuz istekleri (op, outcome).");
        _duration = Meter.CreateHistogram<double>("xslt.request.duration", unit: "ms", description: "Kuyruk dahil uçtan uca süre.");
        _queueWait = Meter.CreateHistogram<double>("xslt.queue.wait", unit: "ms", description: "Worker bekleme süresi.");
        Meter.CreateObservableGauge("xslt.worker.ready", () => _readyProvider(), description: "Hizmete hazır worker sayısı.");
    }

    internal Meter Meter { get; }

    internal void SetReadyProvider(Func<int> provider) => _readyProvider = provider;

    internal void WorkerStarted() => _started.Add(1);

    internal void WorkerExited(string reason) => _exited.Add(1, new KeyValuePair<string, object?>("reason", reason));

    internal void Request(string op, string outcome, double durationMs)
    {
        var tags = new TagList { { "op", op }, { "outcome", outcome } };
        _requests.Add(1, tags);
        _duration.Record(durationMs, tags);
    }

    internal void QueueWait(double milliseconds) => _queueWait.Record(milliseconds);
}
