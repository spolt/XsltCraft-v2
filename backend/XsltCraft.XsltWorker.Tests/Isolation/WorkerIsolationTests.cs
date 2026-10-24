using System.Diagnostics;
using Xunit.Abstractions;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Engine;
using XsltCraft.XsltWorker.Tests.Support;
using static XsltCraft.XsltWorker.Tests.Engine.EngineTestKit;

namespace XsltCraft.XsltWorker.Tests.Isolation;

/// <summary>
/// K4 izolasyon (G0 K-1, SR2, SR4; ADR 008): G0 Crash.cs vektörlerinin TAMAMI gerçek worker sürecine XsltSafety
/// taraması OLMADAN (<c>render-unscreened</c>) verilir. Her vakada: (1) beklenen kind, (2) Api tarafı (bu test süreci)
/// hayatta, (3) havuz sonraki istekte başarılı. Süre eşikleri CI için cömerttir (yalnız üst sınır, kat kat pay).
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class WorkerIsolationTests(ITestOutputHelper output)
{
    // ---- derleme anında reddedilenler (XPath 2.0 tavanı) -----------------------------------------

    [Theory]
    [InlineData(nameof(IsolationVectors.InlineFunctionRecursion))]
    public async Task InlineFunctionRecursion_Unscreened_RejectedAtCompileByXPath20(string vector)
    {
        // G0'da 64 MB'ta bile süreci düşüren satır içi fn özyinelemesi: XPATH_VERSION_FOR_XSLT=20 → derlemede XPST0003,
        // hiç çalışmaz. Worker ölmez. Statik ifadeler (use-when / static / gölge _*) → ayrı test (ön okuma reddi).
        await using var h = await PoolHarness.StartAsync();
        var xslt = (string)typeof(IsolationVectors).GetField(vector)!.GetValue(null)!;

        var result = await Unscreened(h, xslt);

        Assert.Equal(WorkerFailureKinds.Unsupported, result.FailureKind);
        Assert.Equal("XPST0003", result.ErrorCode);
        await AssertSameWorkerHealthy(h, result);
    }

    [Theory]
    [InlineData(nameof(IsolationVectors.UseWhenInlineRecursion))]
    [InlineData(nameof(IsolationVectors.StaticParamInlineRecursion))]
    [InlineData(nameof(IsolationVectors.ShadowSelectInline))]
    public async Task UseWhenInlineRecursion_Unscreened_CrashesWorker_PoolSurvives(string vector)
    {
        // T2.3 BULGUSU (kapatıldı, G2): use-when ifadeleri XPATH_VERSION_FOR_XSLT=20 tavanına tabi değildi ve özyinelemeli
        // sürüm worker'ı düşürüyordu. Artık motor ön okumasında statik ifade reddi (Security) — Saxon'a hiç ulaşmaz.
        // Ad tarihsel (izlenebilirlik); beklenti: Security + aynı worker sağlıklı.
        await using var h = await PoolHarness.StartAsync();
        var xslt = (string)typeof(IsolationVectors).GetField(vector)!.GetValue(null)!;

        var result = await Unscreened(h, xslt);

        Assert.Equal(WorkerFailureKinds.Security, result.FailureKind);
        Assert.Equal("SandboxStylesheetStaticExpression", result.ErrorCode);
        await AssertSameWorkerHealthy(h, result);
    }

    // ---- çalışma anı: listener / regex ---------------------------------------------------------

    [Fact]
    public async Task TryCatchRecursion_Unscreened_ApiPoolSurvives()
    {
        // Yapışkan, xsl:catch'in yakalayamadığı derinlik sınırı (java.lang.Error) → temiz LimitExceeded, süreç yaşar.
        await using var h = await PoolHarness.StartAsync();
        var sw = Stopwatch.StartNew();

        var result = await Unscreened(h, IsolationVectors.TryCatchRecursion);

        Assert.Equal(WorkerFailureKinds.LimitExceeded, result.FailureKind);
        Assert.Equal("LimitTraceDepth", result.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"süre {sw.Elapsed}"); // G0: sıfırlanırsa 75 sn CPU
        await AssertSameWorkerHealthy(h, result);
    }

    [Fact]
    public async Task Redos_Unscreened_RegexLimit_WorkerSurvives()
    {
        await using var h = await PoolHarness.StartAsync();
        var sw = Stopwatch.StartNew();

        var result = await Unscreened(h, IsolationVectors.Redos);

        Assert.Equal(WorkerFailureKinds.LimitExceeded, result.FailureKind);
        Assert.Equal("LimitRegexBacktracking", result.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"süre {sw.Elapsed}");
        await AssertSameWorkerHealthy(h, result);
    }

    [Fact]
    public async Task TransformInnerRecursion_Unscreened_PoolSurvives()
    {
        // fn:transform iç stylesheet'i trace'siz → derinlik sınırı devreye GİRMEZ; 64 MB motor thread'inde yığın
        // taşması süreci düşürür → Api EngineCrashed görür, worker yeniden doğar. (XsltSafety transform'u zaten yasaklar.)
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 30_000);

        var result = await Unscreened(h, IsolationVectors.TransformInnerRecursion, timeout: TimeSpan.FromSeconds(30));

        Assert.Equal(WorkerFailureKinds.EngineCrashed, result.FailureKind);
        await AssertPoolRecovered(h, result);
    }

    // ---- derleme anı derinlik ------------------------------------------------------------------

    [Fact]
    public async Task NestedLre50k_Unscreened_PoolSurvives()
    {
        // G0: 50k iç içe LRE derlemede 82 sn + çökme. Ön okuma derinlik sınırı (istek MaxStylesheetDepth=256) Saxon'dan önce keser.
        await using var h = await PoolHarness.StartAsync();
        var sw = Stopwatch.StartNew();

        var result = await Unscreened(h, IsolationVectors.NestedLre(50_000));

        Assert.Equal(WorkerFailureKinds.LimitExceeded, result.FailureKind);
        Assert.Equal("LimitStylesheetDepth", result.ErrorCode); // G2 #4: ön okuma derinlik ihlali kodlu
        Assert.Contains("256", result.Response.Header.Failure!.Message, StringComparison.Ordinal);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"süre {sw.Elapsed}");
        await AssertSameWorkerHealthy(h, result);
    }

    [Fact]
    public async Task NestedParens50k_Unscreened_PoolSurvives()
    {
        // XPath ayrıştırıcısı özyinelemeli; ifade düzeyinde (parantez) derinlik sınırı motor içinde yok (XsltSafety ≤64).
        // 64 MB derleme thread'inde: çökme → EngineCrashed + yeniden doğuş; Api ayakta.
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 30_000);

        var result = await Unscreened(h, IsolationVectors.NestedParens(50_000), timeout: TimeSpan.FromSeconds(30));

        Assert.Equal(WorkerFailureKinds.EngineCrashed, result.FailureKind);
        await AssertPoolRecovered(h, result);
    }

    // ---- kaynak tüketimi: kill -----------------------------------------------------------------

    [Fact]
    public async Task CpuLoop_KilledByTimeout()
    {
        // enter() üretmeyen XPath döngüsü: işbirlikçi süre çalışamaz → Api sert zaman aşımı → kill + respawn.
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 3_000);
        var sw = Stopwatch.StartNew();

        var result = await Unscreened(h, IsolationVectors.CpuLoop, timeout: TimeSpan.FromSeconds(3));

        Assert.Equal(WorkerFailureKinds.Timeout, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.Timeout, result.ErrorCode); // Api'nin kill'i (worker'ın kendi yanıtı değil)
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"süre {sw.Elapsed}");
        await AssertPoolRecovered(h, result);
    }

    [Fact]
    public async Task MemoryDoubling_KilledByMemoryLimit()
    {
        // 2^40 karakter: GC tavanı (512 MB) → worker'ın kendi OOM yanıtı (worker.out_of_memory + recycle + çıkış; ölçüm:
        // ~0.3 sn) ya da Api bellek bekçisi kill — ikisi de MemoryLimit. Her iki durumda worker süreci biter.
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.GcHeapHardLimitMb = 512;
            o.MaxWorkingSetMb = 768;
            o.RenderTimeoutMs = 30_000;
        });
        var sw = Stopwatch.StartNew();

        var result = await Unscreened(h, IsolationVectors.MemoryDoubling(40), timeout: TimeSpan.FromSeconds(30));

        Assert.Equal(WorkerFailureKinds.MemoryLimit, result.FailureKind);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"süre {sw.Elapsed}");
        await AssertPoolRecovered(h, result);
    }

    // ---- K7: paralel istek izolasyonu ----------------------------------------------------------

    [Fact]
    public async Task ParallelRenders_DistinctInputs_Isolated()
    {
        // Her istek kendi fatura no'su ile; çıktıda YALNIZ kendi no'su (önbellekli executable + paralel worker'lar).
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.PoolSize = 2;
            o.MaxQueue = 64;
            o.QueueWaitMs = 60_000;
        });
        var gib = GibGeneral;
        var invoice = InvoiceXml;
        const string original = "TST2026000000001";
        Assert.Contains(original, invoice);

        var numbers = Enumerable.Range(0, 24).Select(i => $"PAR2026{i:D9}").ToArray();
        var tasks = numbers.Select(no => h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, gib, invoice.Replace(original, no, StringComparison.Ordinal)))).ToArray();
        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < numbers.Length; i++)
        {
            var r = results[i];
            Assert.True(r.Ok, r.Response.Header.Failure?.Message);
            var html = r.Response.Output!;
            Assert.Contains(numbers[i], html);
            Assert.DoesNotContain(original, html);
            foreach (var other in numbers.Where((_, j) => j != i))
                Assert.DoesNotContain(other, html);
        }

        Assert.Equal(2, results.Select(r => r.WorkerPid).Distinct().Count()); // iki worker da iş aldı
        Assert.True(results.Count(r => r.Response.Header.CacheHit) >= numbers.Length - 2);
    }

    // ---- yardımcılar -----------------------------------------------------------------------------

    private async Task<XsltWorkerPoolResult> Unscreened(PoolHarness h, string xslt, string xml = "<r/>", TimeSpan? timeout = null)
    {
        var sw = Stopwatch.StartNew();
        var result = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, xml, op: EngineTestOps.RenderUnscreened) with { Timeout = timeout });
        output.WriteLine($"{result.FailureKind}/{result.ErrorCode} ({sw.ElapsedMilliseconds} ms, pid {result.WorkerPid}): {result.Response.Header.Failure?.Message}");
        return result;
    }

    /// <summary>İstek temiz bir yanıtla bitti: AYNI worker yaşıyor ve sonraki isteğe cevap veriyor.</summary>
    private async Task AssertSameWorkerHealthy(PoolHarness h, XsltWorkerPoolResult failed)
    {
        Assert.NotNull(failed.WorkerPid);
        Assert.False(WorkerTestEnvironment.HasExited(failed.WorkerPid!.Value));
        var next = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, IsolationVectors.Echo, "<r>ok</r>"));
        Assert.True(next.Ok, next.Response.Header.Failure?.Message);
        Assert.Equal("<out>ok</out>", next.Response.Output);
        Assert.Equal(failed.WorkerPid, next.WorkerPid);
        Assert.Equal(0, h.Count(WorkerPoolEventKind.Exited));
    }

    /// <summary>Worker öldü/öldürüldü: Api süreci ayakta, havuz yeni worker'la sonraki isteğe cevap veriyor.</summary>
    private static async Task AssertPoolRecovered(PoolHarness h, XsltWorkerPoolResult failed)
    {
        Assert.False(Process.GetCurrentProcess().HasExited);
        if (failed.WorkerPid is { } pid)
            Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => WorkerTestEnvironment.HasExited(pid), TimeSpan.FromSeconds(15)));

        await h.WaitReadyAsync(seconds: 60);
        var next = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, IsolationVectors.Echo, "<r>ok</r>"));
        Assert.True(next.Ok, next.Response.Header.Failure?.Message);
        Assert.Equal("<out>ok</out>", next.Response.Output);
        Assert.NotEqual(failed.WorkerPid, next.WorkerPid);
    }
}
