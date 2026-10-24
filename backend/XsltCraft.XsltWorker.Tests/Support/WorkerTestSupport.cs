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

/// <summary>Gerçek worker süreçleriyle çalışan havuz + olay kaydı.</summary>
internal sealed class PoolHarness : IAsyncDisposable
{
    private PoolHarness(XsltWorkerPool pool, XsltWorkerOptions options)
    {
        Pool = pool;
        Options = options;
        pool.EventRaised += e => Events.Enqueue(e);
    }

    public XsltWorkerPool Pool { get; }

    public XsltWorkerOptions Options { get; }

    public ConcurrentQueue<WorkerPoolEvent> Events { get; } = new();

    public static async Task<PoolHarness> StartAsync(
        Action<XsltWorkerOptions>? configure = null,
        bool testOps = true,
        string[]? extraArguments = null,
        bool waitReady = true)
    {
        var options = WorkerTestEnvironment.Options(configure);
        IWorkerProcessStartInfoFactory factory = WorkerTestEnvironment.ProductionFactory(options);
        if (testOps)
            factory = new TestOpsStartInfoFactory(factory, extraArguments ?? []);

        var pool = new XsltWorkerPool(
            Microsoft.Extensions.Options.Options.Create(options),
            factory,
            new XsltWorkerMetrics(WorkerTestEnvironment.MeterFactory()),
            NullLogger<XsltWorkerPool>.Instance,
            TimeProvider.System);

        var harness = new PoolHarness(pool, options);
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

    public Task<XsltWorkerPoolResult> RunAsync(string op, long? argument = null, string? hash = null, TimeSpan? timeout = null, IReadOnlyList<string>? bodies = null) =>
        Pool.ExecuteAsync(new XsltWorkerRequest(op, null, hash, bodies ?? []) { TestArgument = argument, Timeout = timeout });

    public IReadOnlyList<WorkerPoolEvent> Snapshot() => Events.ToArray();

    public ValueTask DisposeAsync() => Pool.DisposeAsync();
}
