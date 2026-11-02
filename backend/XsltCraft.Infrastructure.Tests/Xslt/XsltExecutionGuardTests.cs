using System.Diagnostics.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Xslt.InProcess;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.Tests.Xslt;

/// <summary>
/// <see cref="XsltExecutionGuard"/> + Infrastructure gözlemcisi: takılı kaçak (Critical, RestartOnStuckRunaway),
/// bozulmuş mod geçişleri, büyük yığınlı thread, metrikler. Zaman elle ilerletilir; iş kontrollü bloklanır.
/// </summary>
public sealed class XsltExecutionGuardTests : IDisposable
{
    private static readonly XsltGuardTicket Ticket = new("0123456789abcdef", Guid.Parse("22222222-2222-2222-2222-222222222222"), XsltEngineKind.DotNet);

    private readonly ManualResetEventSlim _release = new(false);

    public void Dispose() => _release.Set();

    [Fact]
    public async Task StuckRunaway_LogsCritical_NoRestartByDefault()
    {
        var time = new ManualTime();
        var logger = new CapturingLogger();
        var restarts = 0;
        var guard = new XsltExecutionGuard(new XsltGridOptions(), time, new XsltGuardObserver(logger, () => restarts++));

        var run = guard.RunAsync(Ticket, TimeSpan.FromSeconds(8), _ => Block(), CancellationToken.None);
        await Eventually(() => guard.ActiveCount == 1 && time.PendingTimers > 0);
        time.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal(XsltGuardStatus.Timeout, (await run).Status);

        time.Advance(TimeSpan.FromMinutes(15));
        await Eventually(() => logger.Has(LogLevel.Critical));

        Assert.Equal(0, restarts); // SAHİP KARARI: RestartOnStuckRunaway=false → yalnız Critical
        var critical = logger.Entries.Single(e => e.Level == LogLevel.Critical).Message;
        Assert.Contains("0123456789abcdef", critical);
        Assert.Contains(Ticket.UserId.ToString(), critical);
    }

    [Fact]
    public async Task StuckRunaway_RestartFlag_RequestsRestart()
    {
        var time = new ManualTime();
        var restarts = 0;
        var options = new XsltGridOptions { RestartOnStuckRunaway = true, StuckRunawayMinutes = 1 };
        var guard = new XsltExecutionGuard(options, time, new XsltGuardObserver(new CapturingLogger(), () => Interlocked.Increment(ref restarts)));

        var run = guard.RunAsync(Ticket, TimeSpan.FromSeconds(1), _ => Block(), CancellationToken.None);
        await Eventually(() => guard.ActiveCount == 1 && time.PendingTimers > 0);
        time.Advance(TimeSpan.FromSeconds(1));
        await run;
        time.Advance(TimeSpan.FromMinutes(1));

        await Eventually(() => Volatile.Read(ref restarts) == 1);
    }

    [Fact]
    public async Task FinishedBeforeStuck_NoCritical_DegradedTransitionsLogged()
    {
        var time = new ManualTime();
        var logger = new CapturingLogger();
        var guard = new XsltExecutionGuard(new XsltGridOptions { RunawayMax = 1 }, time, new XsltGuardObserver(logger));

        var run = guard.RunAsync(Ticket, TimeSpan.FromSeconds(1), _ => Block(), CancellationToken.None);
        await Eventually(() => guard.ActiveCount == 1 && time.PendingTimers > 0);
        time.Advance(TimeSpan.FromSeconds(1));
        await run;
        Assert.True(guard.IsDegraded);

        _release.Set();
        await Eventually(() => !guard.IsDegraded && guard.ActiveCount == 0);
        time.Advance(TimeSpan.FromMinutes(30));

        Assert.False(logger.Has(LogLevel.Critical));
        Assert.Contains(logger.Entries, e => e.Message.Contains("bozulmuş moda geçti", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Message.Contains("bozulmuş moddan çıktı", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Work_RunsOnDedicatedLargeStackThread_ExceptionPropagates()
    {
        var guard = new XsltExecutionGuard(new XsltGridOptions(), TimeProvider.System);
        var requestThread = Environment.CurrentManagedThreadId;

        var result = await guard.RunAsync(Ticket, TimeSpan.FromSeconds(5), _ => (Thread.CurrentThread.Name, Environment.CurrentManagedThreadId, Thread.CurrentThread.IsBackground));

        Assert.Equal(XsltGuardStatus.Completed, result.Status);
        Assert.Equal("xslt-inproc", result.Value.Name);
        Assert.NotEqual(requestThread, result.Value.CurrentManagedThreadId);
        Assert.True(result.Value.IsBackground);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.RunAsync<int>(Ticket, TimeSpan.FromSeconds(5), _ => throw new InvalidOperationException("x")));
        Assert.Equal(0, guard.ActiveCount);
    }

    [Fact]
    public async Task DeepRecursion_FitsIn16MbStack()
    {
        // 1 MB'lık istek thread'ini taşıracak derinlik (~ 200–300 B/çerçeve × 20k ≈ 4–6 MB) guard thread'inde sığar.
        var guard = new XsltExecutionGuard(new XsltGridOptions(), TimeProvider.System);

        var result = await guard.RunAsync(Ticket, TimeSpan.FromSeconds(10), _ => Recurse(20_000));

        Assert.Equal(XsltGuardStatus.Completed, result.Status);
        Assert.Equal(20_000, result.Value);
    }

    [Fact]
    public async Task Metrics_InProcessRequests_AndRunawayGauge()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXsltExecution(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<InProcessXsltHost>();
        var guard = provider.GetRequiredService<XsltExecutionGuard>();
        var meter = provider.GetRequiredService<XsltWorkerMetrics>();

        var requests = new List<(string Place, string Outcome)>();
        long runawayGauge = -1;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            // Yalnız bu sağlayıcının meter'ı (paralel testlerin aynı adlı meter'ları karışmasın).
            if (ReferenceEquals(instrument.Meter, meter.Meter))
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "xslt.requests")
            {
                var dict = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
                lock (requests)
                    requests.Add((dict["place"]!, dict["outcome"]!));
            }
        });
        listener.SetMeasurementEventCallback<int>((instrument, value, _, _) =>
        {
            if (instrument.Name == "xslt.inproc.runaway")
                runawayGauge = value;
        });
        listener.Start();

        const string ns = "http://www.w3.org/1999/XSL/Transform";
        var limits = new XsltExecutionLimits { Deadline = TimeSpan.FromSeconds(10), MaxInputDepth = 64, MaxInputNodes = 200_000 };
        var ok = await host.ExecuteAsync(new XsltHostRequest(XsltOperation.Render, XsltEngineKind.DotNet,
            $"<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"{ns}\"><xsl:template match=\"/\"><p/></xsl:template></xsl:stylesheet>", "<r/>", limits));
        Assert.True(ok.Diagnostic is null);

        var outcome = await guard.RunAsync(Ticket, TimeSpan.FromMilliseconds(50), _ => Block());
        Assert.Equal(XsltGuardStatus.Timeout, outcome.Status);
        listener.RecordObservableInstruments();

        Assert.Contains(("inprocess", "ok"), requests);
        Assert.Equal(1, runawayGauge);
    }

    [Fact]
    public void Registration_RestartCallback_OnlyWhenFlagSet()
    {
        Assert.Equal(0, StopCalls(restartFlag: false));
        Assert.Equal(1, StopCalls(restartFlag: true));

        static int StopCalls(bool restartFlag)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Xslt:Grid:RestartOnStuckRunaway"] = restartFlag ? "true" : "false",
            }).Build();
            var lifetime = new FakeLifetime();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostApplicationLifetime>(lifetime);
            services.AddXsltExecution(configuration);
            using var provider = services.BuildServiceProvider();

            var observer = provider.GetRequiredService<IXsltGuardObserver>();
            observer.RunawayStuck(Ticket, TimeSpan.FromMinutes(16), 1, restartRequested: restartFlag);
            return lifetime.StopCalls;
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private int Block()
    {
        _release.Wait(TimeSpan.FromSeconds(30));
        return 0;
    }

    private static int Recurse(int depth)
    {
        Span<byte> pad = stackalloc byte[128];
        pad[0] = 1;
        return depth == 0 ? pad[0] - 1 : 1 + Recurse(depth - 1);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Koşul 10 sn içinde sağlanmadı.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public int StopCalls { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopCalls++;
    }

    private sealed class CapturingLogger : ILogger<XsltGuardObserver>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries)
                    return _entries.ToList();
            }
        }

        public bool Has(LogLevel level) => Entries.Any(e => e.Level == level);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
                _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    /// <summary>
    /// Elle ilerletilen saat (UtcNow + zamanlayıcılar; <c>Task.WaitAsync(timeout, TimeProvider)</c> da bunu kullanır).
    /// <see cref="TimeProvider.GetTimestamp"/> gerçek kalır.
    /// </summary>
    private sealed class ManualTime : TimeProvider
    {
        private readonly object _lock = new();
        private readonly List<Timer> _timers = [];
        private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        public int PendingTimers
        {
            get
            {
                lock (_lock)
                    return _timers.Count;
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_lock)
                return _now;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            List<Timer> due;
            lock (_lock)
            {
                _now += by;
                due = _timers.Where(t => t.DueAt <= _now).ToList();
                foreach (var t in due)
                    _timers.Remove(t);
            }

            foreach (var t in due)
                t.Fire();
        }

        private sealed class Timer(ManualTime owner, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset DueAt { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner._lock)
                {
                    owner._timers.Remove(this);
                    if (dueTime == Timeout.InfiniteTimeSpan)
                        return true;
                    DueAt = owner._now + dueTime;
                    if (dueTime > TimeSpan.Zero)
                    {
                        owner._timers.Add(this);
                        return true;
                    }
                }

                ThreadPool.QueueUserWorkItem(_ => Fire());
                return true;
            }

            public void Fire() => callback(state);

            public void Dispose()
            {
                lock (owner._lock)
                    owner._timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
