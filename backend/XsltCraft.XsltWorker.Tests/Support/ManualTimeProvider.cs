namespace XsltCraft.XsltWorker.Tests.Support;

/// <summary>
/// Elle ilerletilen saat: <see cref="GetUtcNow"/> ve zamanlayıcılar (<c>Task.Delay(…, timeProvider)</c>) yalnız
/// <see cref="Advance"/> ile ilerler. Havuzun yaş/backoff/kesici kararlarını gerçek süre beklemeden sınamak için.
/// <see cref="TimeProvider.GetTimestamp"/> gerçek kalır (süre ölçümleri Stopwatch ile).
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _now;
    }

    /// <summary>Bekleyen (henüz tetiklenmemiş) zamanlayıcı sayısı.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
                return _timers.Count;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = _timers.Where(t => t.DueAt <= _now).ToList();
            foreach (var timer in due)
            {
                if (timer.Period > TimeSpan.Zero)
                    timer.DueAt = _now + timer.Period;
                else
                    _timers.Remove(timer);
            }
        }

        foreach (var timer in due)
            timer.Fire();
    }

    private bool Schedule(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
            if (dueTime == Timeout.InfiniteTimeSpan)
                return false;

            timer.DueAt = _now + dueTime;
            timer.Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
            if (dueTime > TimeSpan.Zero)
            {
                _timers.Add(timer);
                return false;
            }

            if (timer.Period > TimeSpan.Zero)
            {
                timer.DueAt = _now + timer.Period;
                _timers.Add(timer);
            }

            return true; // hemen tetikle
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
            _timers.Remove(timer);
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; set; }

        public TimeSpan Period { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (owner.Schedule(this, dueTime, period))
                ThreadPool.QueueUserWorkItem(_ => Fire());
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
