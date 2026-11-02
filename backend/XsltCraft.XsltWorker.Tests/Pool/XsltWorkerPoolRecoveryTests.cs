using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Pool;

/// <summary>
/// Yeniden başlatma / backoff / yaş tabanlı geri dönüşüm / kuyruk iptali. Saat <see cref="ManualTimeProvider"/>:
/// backoff gecikmesi ve yaş yalnız elle ilerletilince geçer → gerçek süre beklemeden, kararlı sınama.
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public class XsltWorkerPoolRecoveryTests
{
    private static readonly DateTimeOffset FakeEpoch = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Timeout_DoesNotIncreaseBackoff_RespawnImmediate()
    {
        var time = new ManualTimeProvider(FakeEpoch);
        await using var h = await PoolHarness.StartAsync(o =>
        {
            // Backoff uygulanırsa sahte saat ilerlemediği için yeni worker HİÇ başlamaz → test kırılır.
            o.RespawnBackoffInitialMs = 60_000;
            o.RespawnBackoffMaxMs = 60_000;
        }, timeProvider: time);

        for (var i = 0; i < 3; i++)
        {
            await h.WaitReadyAsync();
            var result = await h.RunAsync(TestOpNames.Sleep, argument: 60_000, timeout: TimeSpan.FromMilliseconds(500));
            Assert.Equal(WorkerFailureKinds.Timeout, result.FailureKind);
        }

        await h.WaitReadyAsync();
        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
        Assert.Equal(3, h.Snapshot().Count(e => e.Kind == WorkerPoolEventKind.Exited && e.Reason == WorkerExitReasons.Timeout));
        Assert.Equal(4, h.Count(WorkerPoolEventKind.Launched));
        Assert.Equal(0, time.PendingTimers);
    }

    [Fact]
    public async Task Crash_StillIncreasesBackoff()
    {
        var time = new ManualTimeProvider(FakeEpoch);
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.RespawnBackoffInitialMs = 60_000;
            o.RespawnBackoffMaxMs = 60_000;
            o.BreakerFailureThreshold = 1000;
        }, timeProvider: time);

        // 1. çökme → hemen yeniden başlat.
        Assert.Equal(WorkerFailureKinds.EngineCrashed, (await h.RunAsync(TestOpNames.CrashStackOverflow)).FailureKind);
        await h.WaitReadyAsync();

        // 2. ardışık çökme → backoff (sahte saatle 60 sn): saat ilerlemeden yeni worker başlamaz.
        Assert.Equal(WorkerFailureKinds.EngineCrashed, (await h.RunAsync(TestOpNames.CrashStackOverflow)).FailureKind);
        var launchedBefore = h.Count(WorkerPoolEventKind.Launched);
        Assert.False(await WorkerTestEnvironment.WaitUntilAsync(() => h.Pool.ReadyCount > 0, TimeSpan.FromSeconds(2)));
        Assert.Equal(launchedBefore, h.Count(WorkerPoolEventKind.Launched));
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, (await h.RunAsync(WorkerOps.Ping)).FailureKind);

        time.Advance(TimeSpan.FromSeconds(60));
        await h.WaitReadyAsync();
        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
    }

    [Fact]
    public async Task UnexpectedSpawnException_SlotRecovered()
    {
        ThrowingStartInfoFactory? throwing = null;
        await using var h = await PoolHarness.StartAsync(
            wrapFactory: inner => throwing = new ThrowingStartInfoFactory(inner, failures: 1),
            waitReady: false);

        // Fabrikanın beklenmeyen istisnası başlatma hatasına çevrilir; yuva "Starting"de kalmaz, yenisi başlar.
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(
            () => h.Snapshot().Any(e => e.Kind == WorkerPoolEventKind.StartFailed && e.Reason == WorkerExitReasons.StartFailed),
            TimeSpan.FromSeconds(10)));
        await h.WaitReadyAsync();

        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
        Assert.Equal(2, throwing!.Calls);
        Assert.Equal(1, h.Pool.ReadyCount);
    }

    [Fact]
    public async Task MaxAge_Recycle_WithFakeTime()
    {
        var time = new ManualTimeProvider(FakeEpoch);
        await using var h = await PoolHarness.StartAsync(o => o.MaxWorkerAgeMinutes = 30, timeProvider: time);

        var first = (await h.RunAsync(WorkerOps.Ping)).WorkerPid!.Value;

        // Yaş havuzun saatiyle ölçülür: 29 dk → geri dönüşüm yok.
        time.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(first, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
        Assert.DoesNotContain(h.Snapshot(), e => e.Kind == WorkerPoolEventKind.RecycleScheduled);

        // 31 dk → bu istek eski worker'da tamamlanır, yedek hazır olunca eskisi emekliye ayrılır.
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(first, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
        Assert.Contains(h.Snapshot(), e => e.Kind == WorkerPoolEventKind.RecycleScheduled && e.Pid == first && e.Reason == "max_age");

        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(
            () => h.Snapshot().Any(e => e.Kind == WorkerPoolEventKind.Retired && e.Pid == first), TimeSpan.FromSeconds(30)));
        var next = await h.RunAsync(WorkerOps.Ping);
        Assert.True(next.Ok);
        Assert.NotEqual(first, next.WorkerPid);
    }

    [Fact]
    public async Task QueueCancel_RecordsCanceledOutcome_AndFreesQueueSlot()
    {
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.MaxQueue = 1;
            o.QueueWaitMs = 30_000;
        });

        var outcomes = new ConcurrentQueue<(string Op, string Outcome)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, h.Metrics.Meter) && instrument.Name == "xslt.requests")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string? op = null, outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "op") op = tag.Value as string;
                else if (tag.Key == "outcome") outcome = tag.Value as string;
            }

            outcomes.Enqueue((op ?? "", outcome ?? ""));
        });
        listener.Start();

        var occupying = h.RunAsync(TestOpNames.Sleep, argument: 4000);

        using var cts = new CancellationTokenSource();
        var queued = h.RunAsync(WorkerOps.Ping, cancellationToken: cts.Token);
        Assert.False(queued.IsCompleted);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        Assert.Contains((WorkerOps.Ping, XsltWorkerPool.CanceledOutcome), outcomes);

        // İptal edilen bekleyen kuyruktan çıktı: MaxQueue=1 iken yeni istek kuyruğa kabul edilir (QueueFull değil).
        var next = h.RunAsync(WorkerOps.Ping);
        Assert.True((await occupying).Ok);
        var nextResult = await next;
        Assert.True(nextResult.Ok, nextResult.ErrorCode);
    }
}
