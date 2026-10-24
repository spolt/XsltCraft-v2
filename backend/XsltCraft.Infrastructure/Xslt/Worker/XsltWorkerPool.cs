using System.Diagnostics;
using System.Globalization;
using System.Text;
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
public sealed class XsltWorkerPool : IHostedService, IAsyncDisposable, IDisposable
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

    // ------------------------------------------------------------------ istek yolu

    /// <summary>
    /// Kuyrukta bir worker atanmasını bekler. Dönüş: (worker, null) atandı; (null, hata) zaman aşımı / canlı worker
    /// yok; (null, null) çağıran iptal etti — bekleyen her durumda kuyruktan çıkarılmıştır. Atama ile iptal yarışında
    /// atama kazanır (worker sızmaz; istek çalışır).
    /// </summary>
    private async Task<(PooledWorker? Worker, WorkerResponse? Failure)> WaitForWorkerAsync(
        Waiter waiter, string id, CancellationToken cancellationToken)
    {
        using var stopDelay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(TimeSpan.FromMilliseconds(_options.QueueWaitMs), _time, stopDelay.Token);
        var first = await Task.WhenAny(waiter.Completion.Task, delay).ConfigureAwait(false);
        await stopDelay.CancelAsync().ConfigureAwait(false);

        if (first != waiter.Completion.Task)
        {
            lock (_gate)
            {
                if (waiter.Node?.List is not null)
                {
                    _waiters.Remove(waiter.Node);
                    if (cancellationToken.IsCancellationRequested)
                        return (null, null);
                    var anyBusy = _workers.Exists(w => w.State == PooledWorkerState.Busy);
                    return (null, anyBusy
                        ? Failed(id, WorkerFailureKinds.Busy, WorkerErrorCodes.QueueTimeout, "XSLT motoru meşgul; bekleme süresi doldu.")
                        : Unavailable(id, WorkerErrorCodes.NoReadyWorker));
                }
            }
        }

        // Atama kilit altında yapıldı; görev tamamlanmış durumda.
        var assigned = await waiter.Completion.Task.ConfigureAwait(false);
        return assigned is null ? (null, Unavailable(id, WorkerErrorCodes.NoReadyWorker)) : (assigned, null);
    }

    private WorkerResponse Complete(
        PooledWorker worker, WorkerProcess process, XsltWorkerRequest request, WorkerExchange exchange, string id, long startedAt)
    {
        WorkerResponse response;
        WorkerProcess? dispose = null;
        WorkerProcess? shutdown = null;
        string? exitReason = null;
        string? recycleReason = null;

        lock (_gate)
        {
            if (exchange.WorkerSurvived)
            {
                response = exchange.Status == WorkerExchangeStatus.Completed
                    ? exchange.Response!
                    : Failed(id, WorkerFailureKinds.LimitExceeded, WorkerErrorCodes.RequestTooLarge, "İstek boyutu worker sınırını aşıyor.");

                if (exchange.Status == WorkerExchangeStatus.Completed)
                {
                    worker.Requests++;
                    _consecutiveFailures = 0;
                    if (request.XsltSha256 is { Length: > 0 } hash)
                        worker.Hashes.Touch(hash);

                    recycleReason = RecycleReason(worker, process, response.Header);
                    if (recycleReason is not null && !worker.RetirePending)
                        ScheduleRecycleLocked(worker, recycleReason);
                }

                if (_stopped || !_workers.Contains(worker))
                {
                    _workers.Remove(worker);
                    dispose = process;
                }
                else if (process.HasExited)
                {
                    // Yanıttan hemen sonra çıktı (ör. W2: OOM sonrası kendiliğinden çıkış).
                    exitReason = worker.RetirePending ? WorkerExitReasons.Recycled : WorkerExitReasons.Crashed;
                    RemoveDeadLocked(worker, exitReason, isEngineFailure: !worker.RetirePending);
                    dispose = process;
                }
                else if (worker.RetireAfterCurrent)
                {
                    _workers.Remove(worker);
                    _metrics.WorkerExited(WorkerExitReasons.Recycled);
                    Raise(WorkerPoolEventKind.Retired, worker, WorkerExitReasons.Recycled);
                    shutdown = process;
                }
                else
                {
                    worker.State = PooledWorkerState.Idle;
                    DispatchLocked(worker);
                }
            }
            else
            {
                (response, exitReason) = MapDeath(id, exchange.Status);
                if (!_stopped && _workers.Contains(worker))
                {
                    var isEngineFailure = exchange.Status is WorkerExchangeStatus.Crashed or WorkerExchangeStatus.ProtocolViolation;
                    RemoveDeadLocked(worker, exitReason, isEngineFailure);
                }
                else
                {
                    // Havuz kapanıyor: çıkış metriğini ShutdownAllAsync sayar.
                    _workers.Remove(worker);
                }

                dispose = process;
            }
        }

        if (exitReason is not null && exitReason != WorkerExitReasons.Recycled)
        {
            _logger.LogWarning(
                "XSLT worker sonlandı: neden={Reason} pid={Pid} exitCode={ExitCode} op={Op} xsltHash={XsltHash} süre={DurationMs:F0}ms stderrBayt={StderrBytes} ayrıntı={Detail}",
                exitReason, process.Pid, exchange.ExitCode ?? process.ExitCode, request.Op, HashPrefix(request.XsltSha256),
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, process.StderrBytes, exchange.Detail);
        }

        if (recycleReason is not null)
        {
            _logger.LogInformation("XSLT worker geri dönüşüme alındı: pid={Pid} neden={Reason} istek={Requests}",
                process.Pid, recycleReason, worker.Requests);
        }

        if (shutdown is not null)
            Track(ShutdownAndDisposeAsync(shutdown, "recycle"));
        if (dispose is not null)
            Track(dispose.DisposeAsync().AsTask());

        return response;
    }

    private string? RecycleReason(PooledWorker worker, WorkerProcess process, ResponseHeader header)
    {
        if (header.RecycleRequested)
            return "recycle_requested";
        if (worker.Requests >= _options.MaxRequestsPerWorker)
            return "max_requests";
        if (_time.GetUtcNow() - process.StartedAt >= TimeSpan.FromMinutes(_options.MaxWorkerAgeMinutes))
            return "max_age";
        if (_options.RecycleWorkingSetMb > 0 && header.WorkingSetBytes > _options.RecycleWorkingSetMb * 1024L * 1024L)
            return "working_set";
        return null;
    }

    private XsltWorkerPoolResult Finish(XsltWorkerRequest request, WorkerResponse response, int? pid, long startedAt)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var outcome = response.Header.Ok ? "ok" : response.Header.Failure?.Kind ?? "unknown";
        _metrics.Request(request.Op, outcome, elapsed);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("XSLT worker isteği: op={Op} sonuç={Outcome} pid={Pid} xsltHash={XsltHash} süre={DurationMs:F0}ms",
                request.Op, outcome, pid, HashPrefix(request.XsltSha256), elapsed);
        }

        return new XsltWorkerPoolResult(response, pid);
    }

    private void FinishCanceled(XsltWorkerRequest request, long startedAt)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _metrics.Request(request.Op, CanceledOutcome, elapsed);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("XSLT worker isteği kuyrukta iptal edildi: op={Op} xsltHash={XsltHash} süre={DurationMs:F0}ms",
                request.Op, HashPrefix(request.XsltSha256), elapsed);
        }
    }

    // ------------------------------------------------------------------ havuz durumu (kilit altında)

    private PooledWorker? SelectIdleLocked(string? hash)
    {
        PooledWorker? best = null;
        var bestScore = -1;
        foreach (var worker in _workers)
        {
            if (worker.State != PooledWorkerState.Idle)
                continue;

            var score = (hash is not null && worker.Hashes.Contains(hash) ? 2 : 0) + (worker.RetirePending ? 0 : 1);
            if (score > bestScore || (score == bestScore && worker.LastUsedTicks < best!.LastUsedTicks))
            {
                best = worker;
                bestScore = score;
            }
        }

        return best;
    }

    private void MarkBusyLocked(PooledWorker worker)
    {
        worker.State = PooledWorkerState.Busy;
        worker.LastUsedTicks = Stopwatch.GetTimestamp();

        if (!worker.RetirePending
            && _time.GetUtcNow() - worker.Process!.StartedAt >= TimeSpan.FromMinutes(_options.MaxWorkerAgeMinutes))
        {
            ScheduleRecycleLocked(worker, "max_age");
        }
    }

    private void DispatchLocked(PooledWorker worker)
    {
        if (worker.State != PooledWorkerState.Idle || _waiters.First is not { } node)
            return;

        _waiters.RemoveFirst();
        MarkBusyLocked(worker);
        node.Value.Completion.TrySetResult(worker);
    }

    private bool HasLiveWorkerLocked() =>
        _workers.Exists(w => w.State is PooledWorkerState.Starting or PooledWorkerState.Idle or PooledWorkerState.Busy);

    private void FailWaitersIfNothingLiveLocked()
    {
        if (HasLiveWorkerLocked())
            return;

        while (_waiters.First is { } node)
        {
            _waiters.RemoveFirst();
            node.Value.Completion.TrySetResult(null);
        }
    }

    private void ScheduleRecycleLocked(PooledWorker worker, string reason)
    {
        worker.RetirePending = true;
        Raise(WorkerPoolEventKind.RecycleScheduled, worker, reason);
        EnsureCapacityLocked();
    }

    /// <summary>
    /// Ölü worker'ı listeden çıkarır ve yerine yenisini ister. <paramref name="isEngineFailure"/> yalnız motor
    /// sağlığını gösteren çıkışlarda (çökme, protokol ihlali) <c>true</c>'dur: backoff'u ve kesiciyi YALNIZ bunlar
    /// besler. Kullanıcı kaynaklı Timeout/MemoryLimit öldürmeleri hemen yeniden başlatılır (kapasite boş kalmaz);
    /// onların kötüye kullanımı kullanıcı kapısının (UserGate) işidir.
    /// </summary>
    private void RemoveDeadLocked(PooledWorker worker, string reason, bool isEngineFailure)
    {
        _workers.Remove(worker);
        _metrics.WorkerExited(reason);
        Raise(WorkerPoolEventKind.Exited, worker, reason);

        if (isEngineFailure)
            RecordEngineFailureLocked(worker, reason);

        EnsureCapacityLocked();
        FailWaitersIfNothingLiveLocked();
    }

    /// <summary>Çökme / protokol ihlali / başlatma-el sıkışma hatası: backoff sayacı + kesici.</summary>
    private void RecordEngineFailureLocked(PooledWorker worker, string reason)
    {
        _consecutiveFailures++;
        if (_breaker.RecordFailure(_time.GetUtcNow()))
        {
            Raise(WorkerPoolEventKind.BreakerOpened, worker, reason);
            _logger.LogError("XSLT worker kesicisi AÇILDI: {Window} sn içinde {Threshold} çökme; {Open} sn boyunca istekler reddedilecek.",
                _options.BreakerWindowSeconds, _options.BreakerFailureThreshold, _options.BreakerOpenSeconds);
        }
    }

    /// <summary>Geri dönüşümde olmayan yuva sayısını PoolSize'a tamamlar (gerekirse backoff ile).</summary>
    private void EnsureCapacityLocked()
    {
        if (!_started || _stopped)
            return;

        var missing = _options.PoolSize - _workers.Count(w => !w.RetirePending);
        if (missing <= 0)
            return;

        var delay = NextBackoffLocked();
        for (var i = 0; i < missing; i++)
        {
            var slot = new PooledWorker(
                ++_sequence,
                delay > TimeSpan.Zero ? PooledWorkerState.Scheduled : PooledWorkerState.Starting,
                _options.AffinityHashesPerWorker);
            _workers.Add(slot);
            TrackLocked(Task.Run(() => RunSpawnAsync(slot, delay)));
        }
    }

    /// <summary>İlk hata → hemen; sonrakiler 0.5 sn'den başlayıp 30 sn'ye dek ikiye katlanır.</summary>
    private TimeSpan NextBackoffLocked()
    {
        if (_consecutiveFailures <= 1)
            return TimeSpan.Zero;

        var exponent = Math.Min(_consecutiveFailures - 2, 16);
        var ms = Math.Min((double)_options.RespawnBackoffInitialMs * (1L << exponent), _options.RespawnBackoffMaxMs);
        return TimeSpan.FromMilliseconds(ms);
    }

    /// <summary>Taze (geri dönüşümde olmayan) hizmetteki worker sayısı PoolSize'a ulaştıysa eskileri emekliye ayırır.</summary>
    private void RetireReplacedLocked()
    {
        while (_workers.Count(w => w.IsServing && !w.RetirePending) >= _options.PoolSize)
        {
            var old = _workers
                .Where(w => w.RetirePending && w.IsServing && !w.RetireAfterCurrent)
                .MinBy(w => w.Sequence);
            if (old is null)
                return;

            if (old.State == PooledWorkerState.Busy)
            {
                old.RetireAfterCurrent = true;
                continue;
            }

            _workers.Remove(old);
            _metrics.WorkerExited(WorkerExitReasons.Recycled);
            Raise(WorkerPoolEventKind.Retired, old, WorkerExitReasons.Recycled);
            TrackLocked(ShutdownAndDisposeAsync(old.Process!, "recycle"));
        }
    }

    // ------------------------------------------------------------------ süreç yaşam döngüsü

    private async Task RunSpawnAsync(PooledWorker slot, TimeSpan delay)
    {
        var lifetime = _lifetime.Token;
        if (delay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(delay, _time, lifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                    _workers.Remove(slot);
                return;
            }
        }

        lock (_gate)
        {
            if (_stopped)
            {
                _workers.Remove(slot);
                return;
            }

            slot.State = PooledWorkerState.Starting;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var launched = false;
        WorkerProcess? process = null;
        WorkerStartException? failure = null;
        try
        {
            var spec = _factory.Create();
            process = await WorkerProcess.StartAsync(spec, _options, _time, pid =>
            {
                launched = true;
                _metrics.WorkerStarted();
                lock (_gate)
                    Raise(WorkerPoolEventKind.Launched, slot, null, pid);
            }, lifetime).ConfigureAwait(false);
        }
        catch (WorkerStartException ex)
        {
            failure = ex;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Locator (FileNotFoundException ⊂ IOException) veya dizin hazırlığı.
            failure = new WorkerStartException(WorkerExitReasons.StartFailed, ex.Message, ex);
        }
        catch (Exception ex)
        {
            // Beklenmeyen her istisna (fabrika/başlatma kodu) da başlatma hatasıdır: aksi hâlde yuva kalıcı olarak
            // "Starting"de kalır, kapasite hesabı onu canlı sayar ve havuz yerine yenisini hiç başlatmaz.
            failure = new WorkerStartException(WorkerExitReasons.StartFailed, $"Beklenmeyen başlatma hatası ({ex.GetType().Name}).", ex);
        }

        WorkerProcess? discard = null;
        lock (_gate)
        {
            if (failure is not null)
            {
                _workers.Remove(slot);
                if (launched)
                    _metrics.WorkerExited(failure.Reason);
                Raise(WorkerPoolEventKind.StartFailed, slot, failure.Reason);

                if (!_stopped)
                {
                    RecordEngineFailureLocked(slot, failure.Reason);
                    EnsureCapacityLocked();
                    FailWaitersIfNothingLiveLocked();
                }
            }
            else if (_stopped)
            {
                _workers.Remove(slot);
                discard = process;
            }
            else
            {
                slot.Process = process;
                slot.State = PooledWorkerState.Idle;
                slot.LastUsedTicks = Stopwatch.GetTimestamp();
                Raise(WorkerPoolEventKind.Ready, slot, null);
                RetireReplacedLocked();
                DispatchLocked(slot);
                TrackLocked(MonitorIdleExitAsync(slot, process!));
            }
        }

        if (failure is not null && !_lifetime.IsCancellationRequested)
        {
            _logger.LogWarning("XSLT worker başlatılamadı: neden={Reason} mesaj={Message} ardışıkHata={Failures}",
                failure.Reason, failure.Message, Volatile.Read(ref _consecutiveFailures));
        }
        else if (process is not null && discard is null)
        {
            _logger.LogInformation("XSLT worker hazır: pid={Pid} protokol={Protocol} sürüm={Version} süre={DurationMs:F0}ms",
                process.Pid, process.Hello?.Protocol, process.Hello?.WorkerVersion, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        if (discard is not null)
            await discard.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Boştayken ölen worker'ı (dış kill, OOM katili, W2'de kendiliğinden çıkış) yakalar.</summary>
    private async Task MonitorIdleExitAsync(PooledWorker slot, WorkerProcess process)
    {
        await process.Exited.ConfigureAwait(false);

        string reason;
        lock (_gate)
        {
            // Meşgul worker'ın ölümünü istek yolu sınıflandırır; listeden çıkmış olanı kapatan taraf yönetir.
            if (_stopped || slot.Process != process || slot.State != PooledWorkerState.Idle || !_workers.Contains(slot))
                return;

            reason = slot.RetirePending ? WorkerExitReasons.Recycled : WorkerExitReasons.Crashed;
            RemoveDeadLocked(slot, reason, isEngineFailure: !slot.RetirePending);
        }

        _logger.LogWarning("XSLT worker boştayken sonlandı: pid={Pid} exitCode={ExitCode} neden={Reason}",
            process.Pid, process.ExitCode, reason);
        await process.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task ShutdownAndDisposeAsync(WorkerProcess process, string reason)
    {
        await process.ShutdownAsync(reason).ConfigureAwait(false);
        await process.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ShutdownAllAsync(bool graceful)
    {
        List<PooledWorker> workers;
        List<Waiter> waiters;
        lock (_gate)
        {
            if (_stopped)
            {
                workers = [];
                waiters = [];
            }
            else
            {
                _stopped = true;
                workers = [.. _workers];
                _workers.Clear();
                waiters = [.. _waiters];
                _waiters.Clear();
            }
        }

        if (!_lifetime.IsCancellationRequested)
            await _lifetime.CancelAsync().ConfigureAwait(false);

        foreach (var waiter in waiters)
            waiter.Completion.TrySetResult(null);

        var stops = workers
            .Where(w => w.Process is not null)
            .Select(w => graceful && w.State == PooledWorkerState.Idle
                ? ShutdownAndDisposeAsync(w.Process!, "shutdown")
                : w.Process!.DisposeAsync().AsTask());
        await Task.WhenAll(stops).ConfigureAwait(false);

        foreach (var worker in workers.Where(w => w.Process is not null))
        {
            _metrics.WorkerExited(WorkerExitReasons.Shutdown);
            Raise(WorkerPoolEventKind.Exited, worker, WorkerExitReasons.Shutdown);
        }

        // Başlatılmakta olan süreçler iptal edilip öldürülene dek bekle (Dispose sonrası canlı süreç kalmasın).
        Task[] background;
        lock (_gate)
            background = [.. _background];
        try
        {
            await Task.WhenAll(background)
                .WaitAsync(TimeSpan.FromMilliseconds(_options.StartupTimeoutMs + (2 * _options.ShutdownGraceMs)))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning("XSLT worker havuzu kapanışında arka plan görevleri zamanında bitmedi.");
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private void Track(Task task)
    {
        lock (_gate)
            TrackLocked(task);
    }

    private void TrackLocked(Task task)
    {
        _background.Add(task);
        task.ContinueWith(t =>
        {
            lock (_gate)
                _background.Remove(t);
            if (t.Exception is { } ex)
                _logger.LogError(ex.GetBaseException(), "XSLT worker havuzu arka plan görevi başarısız.");
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void Raise(WorkerPoolEventKind kind, PooledWorker worker, string? reason, int? pid = null)
    {
        try
        {
            EventRaised?.Invoke(new WorkerPoolEvent(kind, worker.Sequence, pid ?? worker.Pid, reason));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WorkerPoolEvent dinleyicisi hata verdi.");
        }
    }

    private static bool TryEncodeBodies(XsltWorkerRequest request, out ReadOnlyMemory<byte>[] bodies)
    {
        try
        {
            bodies = request.Bodies.Select(b => (ReadOnlyMemory<byte>)WorkerWire.EncodeBody(b)).ToArray();
            return true;
        }
        catch (EncoderFallbackException)
        {
            bodies = [];
            return false;
        }
    }

    private static (WorkerResponse Response, string Reason) MapDeath(string id, WorkerExchangeStatus status) => status switch
    {
        WorkerExchangeStatus.Timeout => (Failed(id, WorkerFailureKinds.Timeout, WorkerErrorCodes.Timeout,
            "XSLT işlemi süre sınırını aştı."), WorkerExitReasons.Timeout),
        WorkerExchangeStatus.MemoryLimit => (Failed(id, WorkerFailureKinds.MemoryLimit, WorkerErrorCodes.MemoryLimit,
            "XSLT işlemi bellek sınırını aştı."), WorkerExitReasons.MemoryLimit),
        WorkerExchangeStatus.ProtocolViolation => (Failed(id, WorkerFailureKinds.EngineCrashed, WorkerErrorCodes.ProtocolViolation,
            "XSLT motoru beklenmedik çıktı üretti ve sonlandırıldı."), WorkerExitReasons.ProtocolViolation),
        WorkerExchangeStatus.Shutdown => (Unavailable(id, WorkerErrorCodes.NoReadyWorker), WorkerExitReasons.Shutdown),
        _ => (Failed(id, WorkerFailureKinds.EngineCrashed, WorkerErrorCodes.Crashed,
            "XSLT motoru beklenmedik şekilde sonlandı."), WorkerExitReasons.Crashed),
    };

    private static WorkerResponse Unavailable(string id, string errorCode) =>
        Failed(id, WorkerFailureKinds.EngineUnavailable, errorCode, "XSLT motoru şu anda kullanılamıyor.");

    private static WorkerResponse Failed(string id, string kind, string errorCode, string message) =>
        WorkerResponse.Failed(id, kind, errorCode, message);

    /// <summary>Log için özetin ilk 16 onaltılık hanesi (hex dışı karakter → log enjeksiyonu yok).</summary>
    private static string? HashPrefix(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        var prefix = hash.Length > 16 ? hash[..16] : hash;
        return prefix.All(Uri.IsHexDigit) ? prefix : "invalid";
    }

    private sealed class Waiter
    {
        public TaskCompletionSource<PooledWorker?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LinkedListNode<Waiter>? Node { get; set; }
    }
}
