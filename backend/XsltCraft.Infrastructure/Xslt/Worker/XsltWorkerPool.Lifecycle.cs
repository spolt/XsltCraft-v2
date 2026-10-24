using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace XsltCraft.Infrastructure.Xslt.Worker;

// Süreç yaşam döngüsü: başlatma, boşta ölüm izleme, kapatma ve arka plan görev takibi.
public sealed partial class XsltWorkerPool
{
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
}
