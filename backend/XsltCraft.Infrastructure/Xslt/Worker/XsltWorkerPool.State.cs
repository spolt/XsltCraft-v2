using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace XsltCraft.Infrastructure.Xslt.Worker;

// Havuz durumu: seçim, atama, geri dönüşüm, kapasite ve backoff. Tüm üyeler _gate kilidi ALTINDA çağrılır.
public sealed partial class XsltWorkerPool
{
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
}
