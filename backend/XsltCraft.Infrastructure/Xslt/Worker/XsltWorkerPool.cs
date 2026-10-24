using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// İzole XSLT worker süreç havuzu.
/// <list type="bullet">
/// <item>Açılışta <see cref="XsltWorkerOptions.PoolSize"/> worker paralel ısıtılır; Api açılışı BEKLETİLMEZ.</item>
/// <item>Worker başına aynı anda tek istek; boş worker yoksa en çok <see cref="XsltWorkerOptions.MaxQueue"/>
///   istek <see cref="XsltWorkerOptions.QueueWaitMs"/> bekler → <c>Busy</c>. Canlı worker yoksa → <c>EngineUnavailable</c>.</item>
/// <item>Yumuşak afinite: boş worker'lar arasında XSLT özetini son 16'sında tutan tercih edilir (worker önbelleği).</item>
/// <item>Geri dönüşüm (istek sayısı / yaş / RSS / recycleRequested) spawn-before-retire: yedek hazır olana dek eski hizmet verir.</item>
/// <item>Çökme/öldürme sonrası hemen yeni worker; art arda hatalarda üstel backoff; kayan pencere kesici.</item>
/// </list>
/// İçerik (XSLT/XML/çıktı/stderr) ASLA loglanmaz; yalnız özetin ilk 16 hanesi, PID, çıkış kodu, süre.
/// </summary>
public sealed partial class XsltWorkerPool : IHostedService, IAsyncDisposable, IDisposable
{
    /// <summary><c>xslt.requests{outcome}</c>: kuyruk beklemesi çağıran tarafından iptal edildi.</summary>
    internal const string CanceledOutcome = "canceled";

    private readonly XsltWorkerOptions _options;
    private readonly IWorkerProcessStartInfoFactory _factory;
    private readonly XsltWorkerMetrics _metrics;
    private readonly ILogger<XsltWorkerPool> _logger;
    private readonly TimeProvider _time;
    private readonly WorkerCircuitBreaker _breaker;
    private readonly object _gate = new();
    private readonly List<PooledWorker> _workers = [];
    private readonly LinkedList<Waiter> _waiters = new();
    private readonly HashSet<Task> _background = [];
    private readonly CancellationTokenSource _lifetime = new();
    private long _sequence;
    private long _requestId;
    private int _consecutiveFailures;
    private bool _started;
    private bool _stopped;

    public XsltWorkerPool(
        IOptions<XsltWorkerOptions> options,
        IWorkerProcessStartInfoFactory factory,
        XsltWorkerMetrics metrics,
        ILogger<XsltWorkerPool> logger,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        var errors = _options.Validate();
        if (errors.Count > 0)
            throw new OptionsValidationException(XsltWorkerOptions.SectionName, typeof(XsltWorkerOptions), errors);

        _factory = factory;
        _metrics = metrics;
        _logger = logger;
        _time = timeProvider;
        _breaker = new WorkerCircuitBreaker(
            _options.BreakerFailureThreshold,
            TimeSpan.FromSeconds(_options.BreakerWindowSeconds),
            TimeSpan.FromSeconds(_options.BreakerOpenSeconds));
        _metrics.SetReadyProvider(() => ReadyCount);
    }

    /// <summary>Tanı/test için yaşam döngüsü olayları (kilit altında yükseltilebilir).</summary>
    internal event Action<WorkerPoolEvent>? EventRaised;

    /// <summary>En az bir worker hizmete hazır mı. <c>/health</c> buna BAĞLANMAZ (tasarım gereği).</summary>
    public bool IsReady => ReadyCount > 0;

    public int ReadyCount
    {
        get
        {
            lock (_gate)
                return _workers.Count(w => w.IsServing);
        }
    }

    /// <summary>Hizmetteki worker PID'leri (tanı/test).</summary>
    internal IReadOnlyList<int> ServingPids()
    {
        lock (_gate)
            return _workers.Where(w => w.IsServing && w.Pid is not null).Select(w => w.Pid!.Value).ToList();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("XSLT worker havuzu devre dışı ({Section}:Enabled=false).", XsltWorkerOptions.SectionName);
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (_started || _stopped)
                return Task.CompletedTask;

            _started = true;
            EnsureCapacityLocked();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => ShutdownAllAsync(graceful: true);

    public async ValueTask DisposeAsync()
    {
        await ShutdownAllAsync(graceful: false).ConfigureAwait(false);
        _lifetime.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// Bir isteği worker'da çalıştırır. Hiçbir zaman worker arızası nedeniyle fırlatmaz: arızalar
    /// <see cref="WorkerFailureKinds"/> türünde hata yanıtına çevrilir. <paramref name="cancellationToken"/>
    /// yalnız kuyruk beklemesini iptal eder; çalışan istek iptal edilmez/öldürülmez.
    /// </summary>
    public async Task<XsltWorkerPoolResult> ExecuteAsync(XsltWorkerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = Interlocked.Increment(ref _requestId).ToString(CultureInfo.InvariantCulture);
        var startedAt = Stopwatch.GetTimestamp();

        if (!_options.Enabled)
            return Finish(request, Failed(id, WorkerFailureKinds.EngineUnavailable, WorkerErrorCodes.Disabled, "XSLT worker devre dışı."), null, startedAt);

        if (!TryEncodeBodies(request, out var bodies))
            return Finish(request, Failed(id, WorkerFailureKinds.Xml, WorkerErrorCodes.RequestTooLarge, "Girdi geçerli Unicode metin değil."), null, startedAt);

        if (FrameCodec.ComputeFrameLength(0, bodies) > FrameLimits.MaxRequestFrameBytes)
            return Finish(request, Failed(id, WorkerFailureKinds.LimitExceeded, WorkerErrorCodes.RequestTooLarge, "İstek boyutu worker sınırını aşıyor."), null, startedAt);

        PooledWorker? worker;
        Waiter? waiter = null;
        lock (_gate)
        {
            if (!_started || _stopped)
                return Finish(request, Unavailable(id, WorkerErrorCodes.NoReadyWorker), null, startedAt);
            if (_breaker.IsOpen(_time.GetUtcNow()))
                return Finish(request, Unavailable(id, WorkerErrorCodes.BreakerOpen), null, startedAt);

            worker = SelectIdleLocked(request.XsltSha256);
            if (worker is not null)
            {
                MarkBusyLocked(worker);
            }
            else if (!HasLiveWorkerLocked())
            {
                return Finish(request, Unavailable(id, WorkerErrorCodes.NoReadyWorker), null, startedAt);
            }
            else if (_waiters.Count >= _options.MaxQueue)
            {
                return Finish(request, Failed(id, WorkerFailureKinds.Busy, WorkerErrorCodes.QueueFull, "XSLT motoru meşgul; kuyruk dolu."), null, startedAt);
            }
            else
            {
                waiter = new Waiter();
                waiter.Node = _waiters.AddLast(waiter);
            }
        }

        if (waiter is not null)
        {
            var (assigned, failure) = await WaitForWorkerAsync(waiter, id, cancellationToken).ConfigureAwait(false);

            // Kuyrukta geçen süre her sonuçta (atama / zaman aşımı / iptal) kaydedilir.
            _metrics.QueueWait(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            if (assigned is null && failure is null)
            {
                // Çağıran kuyruk beklemesini iptal etti: bekleyen kuyruktan çıkarıldı; sonuç metriği yine kaydedilir.
                FinishCanceled(request, startedAt);
                throw new OperationCanceledException(cancellationToken);
            }

            if (assigned is null)
                return Finish(request, failure!, null, startedAt);
            worker = assigned;
        }
        else
        {
            _metrics.QueueWait(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        var process = worker!.Process!;
        var header = new RequestHeader
        {
            Id = id,
            Op = request.Op,
            Engine = request.Engine,
            XsltSha256 = request.XsltSha256,
            Limits = request.Limits,
            TestArgument = request.TestArgument,
        };

        var exchange = await process.ExecuteAsync(header, bodies, request.Timeout ?? _options.TimeoutFor(request.Op)).ConfigureAwait(false);
        var response = Complete(worker, process, request, exchange, id, startedAt);
        return Finish(request, response, process.Pid, startedAt);
    }
}
