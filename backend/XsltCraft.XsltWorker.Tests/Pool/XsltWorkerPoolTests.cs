using System.Diagnostics;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Pool;

/// <summary>Gerçek worker süreçleriyle havuz davranışı.</summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public class XsltWorkerPoolTests
{
    private const string HashA = "aaaaaaaaaaaaaaaa1111111111111111aaaaaaaaaaaaaaaa1111111111111111";
    private const string HashB = "bbbbbbbbbbbbbbbb2222222222222222bbbbbbbbbbbbbbbb2222222222222222";

    [Fact]
    public async Task Startup_HandshakeReady()
    {
        await using var h = await PoolHarness.StartAsync(o => o.PoolSize = 2);

        Assert.True(h.Pool.IsReady);
        Assert.Equal(2, h.Pool.ReadyCount);

        var pids = h.Pool.ServingPids();
        Assert.Equal(2, pids.Distinct().Count());
        Assert.DoesNotContain(Environment.ProcessId, pids);
        Assert.All(pids, pid => Assert.False(WorkerTestEnvironment.HasExited(pid)));
        Assert.Equal(2, h.Snapshot().Count(e => e.Kind == WorkerPoolEventKind.Ready));

        var ping = await h.RunAsync(WorkerOps.Ping);
        Assert.True(ping.Ok);
        Assert.Contains(ping.WorkerPid!.Value, pids);
    }

    [Fact]
    public async Task ProtocolMismatch_Rejected()
    {
        await using var h = await PoolHarness.StartAsync(
            o => o.BreakerFailureThreshold = 1000,
            extraArguments: ["--test-hello-protocol", (ProtocolVersion.Current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)],
            waitReady: false);

        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(
            () => h.Snapshot().Any(e => e.Kind == WorkerPoolEventKind.StartFailed && e.Reason == "protocol_mismatch"),
            TimeSpan.FromSeconds(30)));

        Assert.Equal(0, h.Pool.ReadyCount);
        var launched = h.Snapshot().Where(e => e.Kind == WorkerPoolEventKind.Launched && e.Pid is not null).Select(e => e.Pid!.Value).ToList();
        Assert.NotEmpty(launched);
        var firstPid = launched[0];
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => WorkerTestEnvironment.HasExited(firstPid), TimeSpan.FromSeconds(10)));

        var result = await h.RunAsync(WorkerOps.Ping);
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, result.FailureKind);
    }

    [Fact]
    public async Task Timeout_KillsAndRespawns()
    {
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 1000);
        var before = (await h.RunAsync(WorkerOps.Ping)).WorkerPid!.Value;

        var sw = Stopwatch.StartNew();
        var result = await h.RunAsync(TestOpNames.Sleep, argument: 60_000);
        sw.Stop();

        Assert.Equal(WorkerFailureKinds.Timeout, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.Timeout, result.ErrorCode);
        Assert.Equal(before, result.WorkerPid);
        // 1 sn sınır + kill + çıkış bekleme (≤2 sn); 60 sn'lik uykudan çok önce dönmeli. CI payı geniş tutuldu.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"Zaman aşımı çok geç döndü: {sw.Elapsed}.");
        Assert.True(WorkerTestEnvironment.HasExited(before));

        await h.WaitReadyAsync();
        var after = await h.RunAsync(WorkerOps.Ping);
        Assert.True(after.Ok);
        Assert.NotEqual(before, after.WorkerPid);
    }

    [Fact]
    public async Task Crash_EngineCrashed_PoolRecovers()
    {
        await using var h = await PoolHarness.StartAsync();

        var result = await h.RunAsync(TestOpNames.CrashStackOverflow);

        // Yakalanamaz StackOverflow yalnız worker'ı öldürdü; bu test süreci (Api'yi temsil eden) ayakta.
        Assert.Equal(WorkerFailureKinds.EngineCrashed, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.Crashed, result.ErrorCode);
        var crashed = result.WorkerPid!.Value;
        Assert.NotEqual(Environment.ProcessId, crashed);
        Assert.True(WorkerTestEnvironment.HasExited(crashed));
        Assert.False(Process.GetCurrentProcess().HasExited);
        Assert.Contains(h.Snapshot(), e => e.Kind == WorkerPoolEventKind.Exited && e.Reason == "crashed" && e.Pid == crashed);

        await h.WaitReadyAsync();
        var after = await h.RunAsync(WorkerOps.Ping);
        Assert.True(after.Ok);
        Assert.NotEqual(crashed, after.WorkerPid);
    }

    [Fact]
    public async Task MemoryWatchdog_Kills()
    {
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.MaxWorkingSetMb = 150;
            o.RecycleWorkingSetMb = 0; // geri dönüşüm eşiği öldürme eşiğini aşamaz (Validate)
            o.GcHeapHardLimitMb = 0; // GC tavanı değil, Api tarafı bekçisi devreye girsin
            o.RenderTimeoutMs = 30_000;
        });

        var sw = Stopwatch.StartNew();
        var result = await h.RunAsync(TestOpNames.Alloc, argument: 600);

        Assert.Equal(WorkerFailureKinds.MemoryLimit, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.MemoryLimit, result.ErrorCode);
        // Bekçi, 30 sn'lik sert zaman aşımından önce öldürmeli (tür zaten MemoryLimit; süre yalnız üst sınır).
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25), $"Bekçi geç öldürdü: {sw.Elapsed}.");
        Assert.True(WorkerTestEnvironment.HasExited(result.WorkerPid!.Value));

        await h.WaitReadyAsync();
        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
    }

    [Fact]
    public async Task QueueFull_Busy()
    {
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.MaxQueue = 1;
            o.QueueWaitMs = 1500;
        });

        var occupying = h.RunAsync(TestOpNames.Sleep, argument: 5000);
        await Task.Delay(400);

        var queued = h.RunAsync(WorkerOps.Ping);          // kuyruğa girer, 1.5 sn sonra zaman aşımı
        await Task.Delay(300);
        var rejected = await h.RunAsync(WorkerOps.Ping);  // kuyruk dolu → anında Busy

        Assert.Equal(WorkerFailureKinds.Busy, rejected.FailureKind);
        Assert.Equal(WorkerErrorCodes.QueueFull, rejected.ErrorCode);

        var timedOut = await queued;
        Assert.Equal(WorkerFailureKinds.Busy, timedOut.FailureKind);
        Assert.Equal(WorkerErrorCodes.QueueTimeout, timedOut.ErrorCode);

        Assert.True((await occupying).Ok);
        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
    }

    [Fact]
    public async Task Affinity_PrefersHashOwner()
    {
        await using var h = await PoolHarness.StartAsync(o => o.PoolSize = 2);

        var owner = (await h.RunAsync(WorkerOps.Ping, hash: HashA)).WorkerPid;
        for (var i = 0; i < 5; i++)
            Assert.Equal(owner, (await h.RunAsync(WorkerOps.Ping, hash: HashA)).WorkerPid);

        var other = (await h.RunAsync(WorkerOps.Ping, hash: HashB)).WorkerPid;
        Assert.NotEqual(owner, other);
        Assert.Equal(owner, (await h.RunAsync(WorkerOps.Ping, hash: HashA)).WorkerPid);
        Assert.Equal(other, (await h.RunAsync(WorkerOps.Ping, hash: HashB)).WorkerPid);

        // Yumuşak: sahip meşgulse beklemeden diğer boş worker kullanılır. Atama ExecuteAsync'in eşzamanlı
        // bölümünde kilit altında yapılır (sıra = çağrı sırası); süre yerine "sahip hâlâ meşgul" denetlenir.
        var busyOwner = h.RunAsync(TestOpNames.Sleep, argument: 8000, hash: HashA);
        var fallback = await h.RunAsync(WorkerOps.Ping, hash: HashA);
        Assert.True(fallback.Ok);
        Assert.Equal(other, fallback.WorkerPid);
        Assert.False(busyOwner.IsCompleted, "Yedek worker, sahibin işini beklememeliydi.");
        Assert.Equal(owner, (await busyOwner).WorkerPid);
    }

    [Fact]
    public async Task Recycle_SpawnBeforeRetire()
    {
        await using var h = await PoolHarness.StartAsync(o => o.MaxRequestsPerWorker = 2);

        var first = (await h.RunAsync(WorkerOps.Ping)).WorkerPid!.Value;
        Assert.Equal(first, (await h.RunAsync(WorkerOps.Ping)).WorkerPid); // 2. istek → geri dönüşüm kararı

        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(
            () => h.Snapshot().Any(e => e.Kind == WorkerPoolEventKind.Retired && e.Pid == first), TimeSpan.FromSeconds(30)));

        var events = h.Snapshot().ToList();
        var scheduled = events.FindIndex(e => e.Kind == WorkerPoolEventKind.RecycleScheduled && e.Pid == first);
        var replacementReady = events.FindIndex(e => e.Kind == WorkerPoolEventKind.Ready && e.Pid != first);
        var retired = events.FindIndex(e => e.Kind == WorkerPoolEventKind.Retired && e.Pid == first);

        Assert.True(scheduled >= 0);
        Assert.True(replacementReady > scheduled, "Yedek, geri dönüşüm kararından sonra başlamalı.");
        Assert.True(retired > replacementReady, "Eski worker, yedek HAZIR olduktan sonra emekliye ayrılmalı (spawn-before-retire).");

        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => WorkerTestEnvironment.HasExited(first), TimeSpan.FromSeconds(10)));
        var next = await h.RunAsync(WorkerOps.Ping);
        Assert.True(next.Ok);
        Assert.Equal(events[replacementReady].Pid, next.WorkerPid);
    }

    [Fact]
    public async Task StdoutGarbage_ProtocolViolation()
    {
        await using var h = await PoolHarness.StartAsync();

        var result = await h.RunAsync(TestOpNames.StdoutGarbage);

        Assert.Equal(WorkerFailureKinds.EngineCrashed, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.ProtocolViolation, result.ErrorCode);
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => WorkerTestEnvironment.HasExited(result.WorkerPid!.Value), TimeSpan.FromSeconds(5)));

        await h.WaitReadyAsync();
        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
    }

    [Fact]
    public async Task ConsoleWrite_IsSwallowed_NoProtocolViolation()
    {
        await using var h = await PoolHarness.StartAsync();

        var result = await h.RunAsync(TestOpNames.ConsoleWrite);
        Assert.True(result.Ok);
        Assert.Equal(result.WorkerPid, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
    }

    [Fact]
    public async Task StderrFlood_DoesNotBlock()
    {
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 30_000);

        var sw = Stopwatch.StartNew();
        var result = await h.RunAsync(TestOpNames.StderrFlood, argument: 16);

        Assert.True(result.Ok, result.Response.Header.Failure?.Message);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25), $"stderr seli isteği yavaşlattı: {sw.Elapsed}.");
        Assert.Equal(result.WorkerPid, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
    }

    [Fact]
    public async Task Dispose_KillsAll()
    {
        var h = await PoolHarness.StartAsync(o => o.PoolSize = 2);
        var pids = h.Pool.ServingPids();
        Assert.Equal(2, pids.Count);

        var inFlight = h.RunAsync(TestOpNames.Sleep, argument: 60_000);
        await Task.Delay(300);

        await h.DisposeAsync();

        Assert.All(pids, pid => Assert.True(WorkerTestEnvironment.HasExited(pid), $"pid {pid} hâlâ çalışıyor"));
        var aborted = await inFlight.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, aborted.FailureKind);
        Assert.Equal(0, h.Pool.ReadyCount);
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, (await h.RunAsync(WorkerOps.Ping)).FailureKind);
    }

    [Fact]
    public async Task Breaker_OpensAfterRepeatedCrashes()
    {
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.BreakerFailureThreshold = 3;
            o.BreakerWindowSeconds = 60;
            o.BreakerOpenSeconds = 3;
            o.RespawnBackoffInitialMs = 100;
        });

        for (var i = 0; i < 3; i++)
        {
            await h.WaitReadyAsync();
            var crash = await h.RunAsync(TestOpNames.CrashStackOverflow);
            Assert.Equal(WorkerFailureKinds.EngineCrashed, crash.FailureKind);
        }

        Assert.Contains(h.Snapshot(), e => e.Kind == WorkerPoolEventKind.BreakerOpened);
        var rejected = await h.RunAsync(WorkerOps.Ping);
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, rejected.FailureKind);
        Assert.Equal(WorkerErrorCodes.BreakerOpen, rejected.ErrorCode);
        Assert.Null(rejected.WorkerPid);

        // Açık kalma süresi dolunca (worker'lar bu arada ısıtıldı) yeniden hizmet.
        XsltWorkerPoolResult? recovered = null;
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() =>
        {
            recovered = h.RunAsync(WorkerOps.Ping).GetAwaiter().GetResult();
            return recovered.Ok;
        }, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task TestOps_DisabledWithoutFlag_Rejected()
    {
        await using var h = await PoolHarness.StartAsync(testOps: false);

        var result = await h.RunAsync(TestOpNames.CrashStackOverflow);

        Assert.Equal(WorkerFailureKinds.EngineInternal, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.UnknownOp, result.ErrorCode);
        Assert.Equal(result.WorkerPid, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
    }

    [Fact]
    public async Task RenderAndValidate_EnginesLoaded()
    {
        // W2 (T2.2): motorlar yüklü — render/validate artık EngineUnavailable dönmez.
        await using var h = await PoolHarness.StartAsync(testOps: false);
        const string Xslt = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:template match=\"/\">ok</xsl:template></xsl:stylesheet>";

        var render = await h.Pool.ExecuteAsync(new XsltWorkerRequest(WorkerOps.Render, WorkerEngines.Saxon, HashA, [Xslt, "<a/>"]));
        var validate = await h.Pool.ExecuteAsync(new XsltWorkerRequest(WorkerOps.Validate, WorkerEngines.DotNet, HashA, [Xslt]));

        // HashA gövdenin özeti değil → worker bütünlük denetimiyle reddeder (önbelleğe yazmadan); worker sağlıklı.
        Assert.Equal(WorkerFailureKinds.EngineInternal, render.FailureKind);
        Assert.Equal("worker.sha256_mismatch", render.ErrorCode);

        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Xslt)));
        var okRender = await h.Pool.ExecuteAsync(new XsltWorkerRequest(WorkerOps.Render, WorkerEngines.Saxon, sha, [Xslt, "<a/>"]));
        var okValidate = await h.Pool.ExecuteAsync(new XsltWorkerRequest(WorkerOps.Validate, WorkerEngines.DotNet, sha, [Xslt]));

        Assert.True(okRender.Ok, okRender.Response.Header.Failure?.Message);
        Assert.Contains("ok", okRender.Response.Output);
        Assert.True(okValidate.Ok, okValidate.Response.Header.Failure?.Message);
        Assert.Equal(render.WorkerPid, validate.WorkerPid); // worker sağlıklı kaldı
        Assert.Equal(render.WorkerPid, okValidate.WorkerPid);
    }

    [Fact]
    public async Task RequestTooLarge_RejectedWithoutKillingWorker()
    {
        await using var h = await PoolHarness.StartAsync(testOps: false);
        var pid = (await h.RunAsync(WorkerOps.Ping)).WorkerPid;

        var huge = new string('x', FrameLimits.MaxRequestFrameBytes);
        var result = await h.RunAsync(WorkerOps.Render, bodies: [huge, "<a/>"]);

        Assert.Equal(WorkerFailureKinds.LimitExceeded, result.FailureKind);
        Assert.Equal(pid, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
    }
}
