using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;
using static XsltCraft.XsltWorker.Tests.Engine.EngineTestKit;

namespace XsltCraft.XsltWorker.Tests.Engine;

/// <summary>
/// Uçtan uca: Api adaptörü (<see cref="WorkerXsltHost"/>) → havuz → gerçek worker süreci → Saxon / .NET motoru.
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public class EnginePoolTests
{
    [Fact]
    public async Task Hello_ReportsSaxonVersion()
    {
        var options = WorkerTestEnvironment.Options();
        var spec = WorkerTestEnvironment.ProductionFactory(options).Create();
        await using var process = await WorkerProcess.StartAsync(spec, options, TimeProvider.System, onLaunched: null, CancellationToken.None);

        Assert.NotNull(process.Hello);
        Assert.Equal("12.10", process.Hello!.SaxonVersion);
        Assert.Equal(process.Pid, process.Hello.Pid);
    }

    [Fact]
    public async Task Pool_Render_Saxon_GibGeneral()
    {
        await using var h = await PoolHarness.StartAsync(testOps: false);
        var host = Host(h);

        var result = await host.ExecuteAsync(HostRender(XsltEngineKind.Saxon, GibGeneral, InvoiceXml));

        Assert.Null(result.Diagnostic);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine);
        var html = result.Html!;
        Assert.StartsWith("<!DOCTYPE html", html.TrimStart());
        Assert.Contains("TST2026000000001", html);                    // fatura no
        Assert.Contains("111d1111-0c11-01bb-123e-1f2f34b567f8", html);  // ETTN
        Assert.Contains("Satış Temsilcisi: Semih Polat", html);        // UTF-8 / Türkçe tel üzerinden bozulmadı
        Assert.NotNull(result.Timings);

        // Aynı şablon ikinci kez → worker önbelleği (afinite aynı worker'a yönlendirir).
        var again = await host.ExecuteAsync(HostRender(XsltEngineKind.Saxon, GibGeneral, InvoiceXml));
        Assert.True(again.CacheHit);
        Assert.Equal(html, again.Html);
    }

    [Fact]
    public async Task Pool_Render_DotNet_V1()
    {
        await using var h = await PoolHarness.StartAsync(testOps: false);
        var host = Host(h);
        var xslt = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:output method="html" encoding="UTF-8" indent="no"/>
              <xsl:template match="/"><p><xsl:value-of select="count(r/i)"/>-<xsl:value-of select="translate(r/@n, 'ı', 'I')"/></p></xsl:template>
            </xsl:stylesheet>
            """;

        var result = await host.ExecuteAsync(HostRender(XsltEngineKind.DotNet, xslt, "<r n=\"çığ\"><i/><i/></r>"));

        Assert.Null(result.Diagnostic);
        Assert.Equal(XsltEngineKind.DotNet, result.Engine);
        Assert.Equal("<p>2-çIğ</p>", result.Html);

        // 1.0 .NET motoru 2.0 fonksiyonunu derleyemez → Compile (yönlendirme Api'nin işi; worker istenen motoru kullanır).
        var v2 = await host.ExecuteAsync(HostValidate(XsltEngineKind.DotNet, Stylesheet("<xsl:value-of select=\"upper-case('a')\"/>", version: "1.0")));
        Assert.Equal(XsltFailureKind.Compile, v2.Diagnostic?.Kind);
    }

    [Fact]
    public async Task Pool_Validate_Saxon_CompileError_LineColumn()
    {
        await using var h = await PoolHarness.StartAsync(testOps: false);
        var host = Host(h);
        var xslt = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <out>
                  <xsl:value-of select="$undeclared"/>
                </out>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = await host.ExecuteAsync(HostValidate(XsltEngineKind.Saxon, xslt));

        var diagnostic = Assert.IsType<XsltDiagnostic>(result.Diagnostic);
        Assert.Equal(XsltFailureKind.Compile, diagnostic.Kind);
        Assert.Equal(XsltEngineKind.Saxon, diagnostic.Engine);
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal("XPST0008", diagnostic.ErrorCode);
        Assert.DoesNotContain("net.sf.saxon", diagnostic.Message, StringComparison.Ordinal);

        // Saxon XPath ifade hatalarında kolon vermez (0 → telde gönderilmez); eleman/öznitelik hatalarında verir.
        var attrError = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <xsl:value-of select="1" foo="x"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var attr = Assert.IsType<XsltDiagnostic>((await host.ExecuteAsync(HostValidate(XsltEngineKind.Saxon, attrError))).Diagnostic);
        Assert.Equal(XsltFailureKind.Compile, attr.Kind);
        Assert.Equal(3, attr.Line);
        Assert.True(attr.Column > 0, $"kolon: {attr.Column}");
    }

    [Fact]
    public async Task Pool_Render_InfiniteRecursion_WorkerKilledOrLimit_PoolRecovers()
    {
        await using var h = await PoolHarness.StartAsync(o => o.RenderTimeoutMs = 10_000);

        var result = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, InfiniteRecursion, "<r/>", op: EngineTestOps.RenderUnscreened));

        // Beklenen: listener → LimitExceeded (worker yaşar). Kabul edilebilir: yığın taşması/kill → EngineCrashed/Timeout.
        Assert.Contains(result.FailureKind, new[] { WorkerFailureKinds.LimitExceeded, WorkerFailureKinds.EngineCrashed, WorkerFailureKinds.Timeout });

        await h.WaitReadyAsync();
        var next = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, Stylesheet("<xsl:value-of select=\"/r\"/>"), "<r>ok</r>"));
        Assert.True(next.Ok, next.Response.Header.Failure?.Message);
        Assert.Equal("<out>ok</out>", next.Response.Output);
    }

    [Fact]
    public async Task Pool_Render_JavaStdout_NotCorruptingProtocol()
    {
        await using var h = await PoolHarness.StartAsync();

        // xsl:message (terminate=no), trace() ve derleme uyarısı: hiçbiri protokol akışına sızmamalı.
        var noisy = Stylesheet(
            "<xsl:message>MESAJ_STDOUT_OLMAMALI</xsl:message><xsl:value-of select=\"trace('TRACE_DEGERI', 'etiket')\"/><a><xsl:variable name=\"unused\" select=\"1\"/></a>");
        var first = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, noisy, "<r/>"));
        Assert.True(first.Ok, first.Response.Header.Failure?.Message);
        Assert.Contains("TRACE_DEGERI", first.Response.Output);
        Assert.DoesNotContain("MESAJ_STDOUT_OLMAMALI", first.Response.Output);

        // Java System.out/err'e doğrudan yazım: JavaStdio olmasa fd 1 kirlenir → Api protokol ihlali → kill.
        var java = await h.RunAsync(EngineTestOps.JavaStdout);
        Assert.True(java.Ok);

        var terminate = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, Stylesheet("<xsl:message terminate=\"yes\">SON</xsl:message>"), "<r/>"));
        Assert.False(terminate.Ok);
        Assert.NotEqual(WorkerFailureKinds.EngineCrashed, terminate.FailureKind);

        var after = await h.Pool.ExecuteAsync(Render(WorkerEngines.Saxon, noisy, "<r/>"));
        Assert.True(after.Ok);

        var pids = new[] { first, java, terminate, after }.Select(r => r.WorkerPid).Distinct().ToList();
        Assert.Single(pids); // aynı worker; hiçbir ihlal/kill yok
        Assert.Equal(0, h.Count(WorkerPoolEventKind.Exited));
        Assert.DoesNotContain(h.Snapshot(), e => e.Reason == WorkerExitReasons.ProtocolViolation);
    }
}
