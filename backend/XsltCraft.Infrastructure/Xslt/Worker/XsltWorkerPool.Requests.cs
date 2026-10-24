using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

// İstek yolu: kuyruk beklemesi, yanıtın sınıflandırılması, sonuç/metrik ve yanıt üreticileri.
public sealed partial class XsltWorkerPool
{
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
