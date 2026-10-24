using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;
using static XsltCraft.XsltWorker.Tests.Engine.EngineTestKit;

namespace XsltCraft.XsltWorker.Tests.Engine;

/// <summary>
/// Worker içi RequestHandler / motor thread'i / derleme önbelleği davranışı — gerçek worker süreci üzerinden
/// (worker derlemesi test projesine referans edilmez; tel sözleşmesi sınanır).
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public class RequestHandlerTests
{
    [Fact]
    public async Task Validate_CompileOnly()
    {
        await using var h = await PoolHarness.StartAsync();
        var xslt = Stylesheet("<xsl:value-of select=\"string-join(('a','b'), '-')\"/>");

        var result = await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, xslt));

        Assert.True(result.Ok, result.Response.Header.Failure?.Message);
        Assert.Null(result.Response.Output); // validate gövde döndürmez
        var header = result.Response.Header;
        Assert.Equal(WorkerEngines.Saxon, header.Engine);
        Assert.False(header.CacheHit);
        Assert.NotNull(header.Timings);
        Assert.True(header.Timings!.Compile > 0);
        Assert.Equal(0, header.Timings.Transform);
        Assert.True(header.WorkingSetBytes > 0);
        Assert.True(header.GcHeapBytes > 0);

        // Çalışma hatası yalnız dönüşümde ortaya çıkar → validate onu görmez (yalnız derleme).
        var runtimeError = Stylesheet("<xsl:value-of select=\"error(QName('urn:x', 'BOOM'), 'patla')\"/>");
        Assert.True((await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, runtimeError))).Ok);
        var rendered = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, runtimeError, "<r/>"));
        Assert.Equal(WorkerFailureKinds.Runtime, rendered.FailureKind);
    }

    [Fact]
    public async Task CacheHit_SecondRequest()
    {
        await using var h = await PoolHarness.StartAsync();
        var xslt = Stylesheet("<xsl:value-of select=\"upper-case(/r)\"/>");

        var validate = await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, xslt));
        var first = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>abc</r>"));
        var second = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>xyz</r>"));

        Assert.True(validate.Ok);
        Assert.False(validate.Response.Header.CacheHit);
        // validate'in derlediği executable render'da yeniden kullanılır.
        Assert.True(first.Response.Header.CacheHit);
        Assert.Equal(0, first.Response.Header.Timings!.Compile);
        Assert.Equal("<out>ABC</out>", first.Response.Output);
        Assert.True(second.Response.Header.CacheHit);
        Assert.Equal("<out>XYZ</out>", second.Response.Output);

        // Anahtar motoru içerir: aynı metin .NET ile ayrı derlenir (1.0 stylesheet).
        var v1 = Stylesheet("<xsl:value-of select=\"/r\"/>", version: "1.0");
        var dotnet1 = await h.Pool.ExecuteAsync(Render(WorkerEngines.DotNet, v1, "<r>1</r>"));
        var saxon1 = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, v1, "<r>1</r>"));
        var dotnet2 = await h.Pool.ExecuteAsync(Render(WorkerEngines.DotNet, v1, "<r>2</r>"));
        Assert.False(dotnet1.Response.Header.CacheHit);
        Assert.False(saxon1.Response.Header.CacheHit);
        Assert.True(dotnet2.Response.Header.CacheHit);
        Assert.Equal(new[] { WorkerEngines.DotNet, WorkerEngines.Saxon, WorkerEngines.DotNet },
            new[] { dotnet1, saxon1, dotnet2 }.Select(r => r.Response.Header.Engine));

        // Başarısız derleme önbelleğe girmez.
        var broken = Stylesheet("<xsl:value-of select=\"1 +\"/>");
        Assert.Equal(WorkerFailureKinds.Compile, (await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, broken))).FailureKind);
        var again = await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, broken));
        Assert.Equal(WorkerFailureKinds.Compile, again.FailureKind);
        Assert.False(again.Response.Header.CacheHit);

        // Daha sıkı stylesheet derinlik sınırı gevşek sınırla derlenmiş girdiyi kullanmaz.
        var strict = Render(WorkerEngines.Saxon, xslt, "<r>q</r>") with { Limits = Limits() with { MaxStylesheetDepth = 100 } };
        var strictResult = await h.Pool.ExecuteAsync(strict);
        Assert.True(strictResult.Ok);
        Assert.False(strictResult.Response.Header.CacheHit);
    }

    [Fact]
    public async Task Sha256Mismatch_Rejected()
    {
        await using var h = await PoolHarness.StartAsync();
        var xslt = Stylesheet("<xsl:value-of select=\"/r\"/>");
        var other = Sha256(xslt + " ");

        var mismatch = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>1</r>", sha256: other));
        Assert.Equal(WorkerFailureKinds.EngineInternal, mismatch.FailureKind);
        Assert.Equal("worker.sha256_mismatch", mismatch.ErrorCode);
        Assert.Null(mismatch.Response.Output);

        // Uyuşmazlık önbelleğe yazmadı: doğru özetli ilk istek yeniden derler.
        var ok = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>1</r>"));
        Assert.True(ok.Ok);
        Assert.False(ok.Response.Header.CacheHit);

        // Büyük harfli hex de geçerli özet; worker sağlıklı kaldı.
        var upper = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>2</r>", sha256: Sha256(xslt).ToUpperInvariant()));
        Assert.True(upper.Ok);
        Assert.True(upper.Response.Header.CacheHit);
        Assert.Equal(mismatch.WorkerPid, upper.WorkerPid);
    }

    [Fact]
    public async Task Oom_RequestsRecycle()
    {
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.GcHeapHardLimitMb = 256; // GC tavanı → worker içinde OutOfMemoryException
            o.MaxWorkingSetMb = 0;     // Api bekçisi kapalı: öldürme değil, worker'ın kendi yanıtı sınanır
            o.RecycleWorkingSetMb = 0;
            o.RenderTimeoutMs = 30_000;
        });

        var result = await h.RunAsync(TestOpNames.Alloc, argument: 1024);

        Assert.Equal(WorkerFailureKinds.MemoryLimit, result.FailureKind);
        Assert.Equal(WorkerErrorCodes.OutOfMemory, result.ErrorCode);
        Assert.True(result.Response.Header.RecycleRequested);

        // Yanıttan sonra worker kendiliğinden çıkar (MemoryExhausted = 4); havuz bunu çökme değil geri dönüşüm sayar.
        var pid = result.WorkerPid!.Value;
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => WorkerTestEnvironment.HasExited(pid), TimeSpan.FromSeconds(10)));
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(
            () => h.Snapshot().Any(e => e.Kind == WorkerPoolEventKind.RecycleScheduled && e.Pid == pid), TimeSpan.FromSeconds(5)));

        await h.WaitReadyAsync();
        var after = await h.RunAsync(WorkerOps.Ping);
        Assert.True(after.Ok);
        Assert.NotEqual(pid, after.WorkerPid);
        Assert.DoesNotContain(h.Snapshot(), e => e.Pid == pid && e.Reason == WorkerExitReasons.Crashed);
    }

    [Fact]
    public async Task Threat_RejectedBeforeEngine()
    {
        await using var h = await PoolHarness.StartAsync();

        // document() → Security; satır/kolon tarayıcıdan. Motor bu içeriği hiç görmez.
        var threat = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <xsl:copy-of select="doc('file:///etc/passwd')"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var result = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, threat, "<r/>"));
        Assert.Equal(WorkerFailureKinds.Security, result.FailureKind);
        Assert.False(string.IsNullOrEmpty(result.ErrorCode));
        Assert.Equal(3, result.Response.Header.Failure!.Line);
        Assert.True(result.Response.Header.Failure.Column > 0);
        Assert.Null(result.Response.Header.Timings); // derleme yapılmadı

        // 2.0 üstü yapı → Unsupported (Execute modu).
        var v3 = Stylesheet("<xsl:iterate select=\"1 to 3\"><xsl:value-of select=\".\"/></xsl:iterate>", version: "3.0");
        var unsupported = await h.Pool.ExecuteAsync(Validate(WorkerEngines.Saxon, v3));
        Assert.Equal(WorkerFailureKinds.Unsupported, unsupported.FailureKind);

        // .NET motoru da aynı kapıdan geçer.
        var dotnet = await h.Pool.ExecuteAsync(Validate(WorkerEngines.DotNet, threat));
        Assert.Equal(WorkerFailureKinds.Security, dotnet.FailureKind);
        Assert.Equal(result.WorkerPid, dotnet.WorkerPid);
    }

    [Fact]
    public async Task Unscreened_TestOpOnly()
    {
        // Saxon sandbox'ı, tarayıcı olmadan da doc()'u reddeder → Security (ama sandbox kodu ile).
        var threat = Stylesheet("<xsl:copy-of select=\"doc('file:///etc/passwd')\"/>");

        await using (var testWorker = await PoolHarness.StartAsync())
        {
            var screened = await testWorker.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, threat, "<r/>"));
            var unscreened = await testWorker.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, threat, "<r/>", op: EngineTestOps.RenderUnscreened));

            Assert.Equal(WorkerFailureKinds.Security, screened.FailureKind);
            Assert.Equal(WorkerFailureKinds.Security, unscreened.FailureKind);
            Assert.NotEqual(screened.ErrorCode, unscreened.ErrorCode); // tarayıcı kodu ≠ Saxon sandbox kodu

            var ok = await testWorker.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, Stylesheet("<xsl:value-of select=\"/r\"/>"), "<r>u</r>", op: EngineTestOps.RenderUnscreened));
            Assert.Equal("<out>u</out>", ok.Response.Output);
        }

        // Üretim worker'ı (--test-ops yok) bu op'u tanımaz.
        await using var production = await PoolHarness.StartAsync(testOps: false);
        var rejected = await production.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, threat, "<r/>", op: EngineTestOps.RenderUnscreened));
        Assert.Equal(WorkerFailureKinds.EngineInternal, rejected.FailureKind);
        Assert.Equal(WorkerErrorCodes.UnknownOp, rejected.ErrorCode);
    }

    [Fact]
    public async Task EngineWork_RunsOnEngineThread_DeepRecursionHitsLimitNotStack()
    {
        await using var h = await PoolHarness.StartAsync();

        var info = await h.RunAsync(EngineTestOps.ThreadInfo);
        Assert.Equal("name=xslt-engine", info.Response.Output);

        // 64 MB yığında Isolated trace derinlik sınırı (1000) yığından önce dolar → temiz LimitExceeded, süreç yaşar.
        var deep = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, InfiniteRecursion, "<r/>", op: EngineTestOps.RenderUnscreened));
        Assert.Equal(WorkerFailureKinds.LimitExceeded, deep.FailureKind);
        Assert.Equal(info.WorkerPid, deep.WorkerPid);
        Assert.False(WorkerTestEnvironment.HasExited(deep.WorkerPid!.Value));
    }

    [Fact]
    public async Task CooperativeDeadline_TimeoutWithoutKill()
    {
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 20_000);
        var loop = Stylesheet(
            "<xsl:for-each select=\"1 to 2000000000\"><xsl:call-template name=\"t\"/></xsl:for-each>",
            top: "<xsl:template name=\"t\"><xsl:sequence select=\"()\"/></xsl:template>");
        var request = Render(WorkerEngines.Saxon, loop, "<r/>") with { Limits = Limits(deadlineMs: 500), Timeout = TimeSpan.FromSeconds(20) };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await h.Pool.ExecuteAsync(request);

        // Worker'ın kendi (işbirlikçi) süresi: temiz Timeout yanıtı, Api'nin sert sınırından (20 sn) çok önce; kill yok.
        Assert.Equal(WorkerFailureKinds.Timeout, result.FailureKind);
        Assert.NotEqual(WorkerErrorCodes.Timeout, result.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"süre: {sw.Elapsed}");
        Assert.False(WorkerTestEnvironment.HasExited(result.WorkerPid!.Value));
        Assert.Equal(result.WorkerPid, (await h.RunAsync(WorkerOps.Ping)).WorkerPid);
    }

    [Fact]
    public async Task UnknownEngine_And_BadBodies_DoNotKillWorker()
    {
        await using var h = await PoolHarness.StartAsync();
        var xslt = Stylesheet("<xsl:value-of select=\"/r\"/>");

        var unknown = await h.Pool.ExecuteAsync(Render("xalan", xslt, "<r/>"));
        Assert.Equal(WorkerFailureKinds.EngineUnavailable, unknown.FailureKind);
        Assert.Equal(WorkerErrorCodes.EngineNotLoaded, unknown.ErrorCode);

        var missingXml = await h.Pool.ExecuteAsync(new XsltWorkerRequest(WorkerOps.Render, WorkerEngines.Saxon, Sha256(xslt), [xslt]) { Limits = Limits() });
        Assert.Equal(WorkerFailureKinds.EngineInternal, missingXml.FailureKind);
        Assert.Equal("worker.bad_request", missingXml.ErrorCode);

        var badXml = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, xslt, "<r>"));
        Assert.Equal(WorkerFailureKinds.Xml, badXml.FailureKind);

        Assert.True((await h.RunAsync(WorkerOps.Ping)).Ok);
        Assert.Equal(unknown.WorkerPid, badXml.WorkerPid);
    }
}
