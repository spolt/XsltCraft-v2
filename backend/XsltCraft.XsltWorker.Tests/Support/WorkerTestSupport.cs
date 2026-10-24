using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.XsltWorker.Tests.Support;

/// <summary>Test op adları (worker'daki <c>Hosting.TestOps</c> ile aynı).</summary>
internal static class TestOpNames
{
    public const string CrashStackOverflow = "crash-stackoverflow";
    public const string Sleep = "sleep";
    public const string Alloc = "alloc";
    public const string StdoutGarbage = "stdout-garbage";
    public const string StderrFlood = "stderr-flood";
    public const string ConsoleWrite = "console-write";
    public const string GcInfo = "gc-info";
    public const string EnvReport = "env-report";
}

/// <summary>
/// TEST KANCASI: üretim fabrikasının ürettiği başlatma bilgisine <c>--test-ops</c> (+ ek argüman) ekler.
/// Üretim kodu bu bayrağı asla eklemez (bkz. WorkerProcessStartInfoTests.TestOps_NeverPassed).
/// </summary>
internal sealed class TestOpsStartInfoFactory(IWorkerProcessStartInfoFactory inner, params string[] extraArguments)
    : IWorkerProcessStartInfoFactory
{
    public WorkerLaunchSpec Create()
    {
        var spec = inner.Create();
        spec.StartInfo.ArgumentList.Add("--test-ops");
        foreach (var argument in extraArguments)
            spec.StartInfo.ArgumentList.Add(argument);
        return spec;
    }
}

internal static class WorkerTestEnvironment
{
    public static XsltWorkerOptions Options(Action<XsltWorkerOptions>? configure = null)
    {
        var options = new XsltWorkerOptions
        {
            PoolSize = 1,
            StartupTimeoutMs = 20_000,
            RenderTimeoutMs = 15_000,
            ValidateTimeoutMs = 15_000,
            QueueWaitMs = 5_000,
        };
        configure?.Invoke(options);
        return options;
    }

    public static WorkerProcessStartInfoFactory ProductionFactory(XsltWorkerOptions options) =>
        new(Microsoft.Extensions.Options.Options.Create(options), new XsltWorkerLocator(Microsoft.Extensions.Options.Options.Create(options)));

    public static IMeterFactory MeterFactory() =>
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    public static bool HasExited(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (condition())
                return true;
            await Task.Delay(50);
        }

        return condition();
    }
}

/// <summary>
/// TEST KANCASI: ilk <c>failures</c> çağrıda beklenmeyen bir istisna fırlatır (havuzun dar <c>catch</c>
/// listesinde olmayan tür), sonra iç fabrikaya devreder.
/// </summary>
internal sealed class ThrowingStartInfoFactory(IWorkerProcessStartInfoFactory inner, int failures) : IWorkerProcessStartInfoFactory
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public WorkerLaunchSpec Create()
    {
        if (Interlocked.Increment(ref _calls) <= failures)
            throw new ArgumentException("Sahte fabrika hatası.");
        return inner.Create();
    }
}

/// <summary>Gerçek worker süreçleriyle çalışan havuz + olay kaydı.</summary>
internal sealed class PoolHarness : IAsyncDisposable
{
    private PoolHarness(XsltWorkerPool pool, XsltWorkerOptions options, XsltWorkerMetrics metrics)
    {
        Pool = pool;
        Options = options;
        Metrics = metrics;
        pool.EventRaised += e => Events.Enqueue(e);
    }

    public XsltWorkerPool Pool { get; }

    public XsltWorkerMetrics Metrics { get; }

    public XsltWorkerOptions Options { get; }

    public ConcurrentQueue<WorkerPoolEvent> Events { get; } = new();

    public static async Task<PoolHarness> StartAsync(
        Action<XsltWorkerOptions>? configure = null,
        bool testOps = true,
        string[]? extraArguments = null,
        bool waitReady = true,
        TimeProvider? timeProvider = null,
        Func<IWorkerProcessStartInfoFactory, IWorkerProcessStartInfoFactory>? wrapFactory = null)
    {
        var options = WorkerTestEnvironment.Options(configure);
        IWorkerProcessStartInfoFactory factory = WorkerTestEnvironment.ProductionFactory(options);
        if (testOps)
            factory = new TestOpsStartInfoFactory(factory, extraArguments ?? []);
        if (wrapFactory is not null)
            factory = wrapFactory(factory);

        var metrics = new XsltWorkerMetrics(WorkerTestEnvironment.MeterFactory());
        var pool = new XsltWorkerPool(
            Microsoft.Extensions.Options.Options.Create(options),
            factory,
            metrics,
            NullLogger<XsltWorkerPool>.Instance,
            timeProvider ?? TimeProvider.System);

        var harness = new PoolHarness(pool, options, metrics);
        await pool.StartAsync(CancellationToken.None);
        if (waitReady)
            await harness.WaitReadyAsync(options.PoolSize);
        return harness;
    }

    public async Task WaitReadyAsync(int count = 1, int seconds = 30)
    {
        var ok = await WorkerTestEnvironment.WaitUntilAsync(() => Pool.ReadyCount >= count, TimeSpan.FromSeconds(seconds));
        Assert.True(ok, $"Havuz {seconds} sn içinde {count} hazır worker'a ulaşmadı (hazır={Pool.ReadyCount}).");
    }

    public Task<XsltWorkerPoolResult> RunAsync(
        string op,
        long? argument = null,
        string? hash = null,
        TimeSpan? timeout = null,
        IReadOnlyList<string>? bodies = null,
        CancellationToken cancellationToken = default) =>
        Pool.ExecuteAsync(new XsltWorkerRequest(op, null, hash, bodies ?? []) { TestArgument = argument, Timeout = timeout }, cancellationToken);

    public int Count(WorkerPoolEventKind kind) => Events.Count(e => e.Kind == kind);

    public IReadOnlyList<WorkerPoolEvent> Snapshot() => Events.ToArray();

    public ValueTask DisposeAsync() => Pool.DisposeAsync();
}
