using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt.InProcess;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Engine;
using XsltCraft.XsltWorker.Tests.Support;
using static XsltCraft.XsltWorker.Tests.Engine.EngineTestKit;

namespace XsltCraft.XsltWorker.Tests.Measurement;

/// <summary>
/// ADR 008 "Ölçümler" (CI dışı, <c>Category=Measurement</c>): grid profili kalan riski ve gecikme tabanı.
/// <list type="bullet">
/// <item>Tasarım sınırları içindeki (uzunluk ≤512, parantez ≤16, iç içe yüklem ≤3, yüklemde tek N-sınıfı eksen)
///   "en kötü kabul edilen" XPath 1.0 ifadesinin süresi — 200k düğüm / derinlik 64 / ~6M karakter XML, .NET ve Saxon.
///   Gerçek worker'da (kill ile sınırlı) koşar: süreç içi grid'de aynı CPU iptal edilemez kaçak thread olurdu.</item>
/// <item>6M karakterlik (base64 görsel ağırlıklı) stylesheet'in süreç içi derleme bellek maliyeti.</item>
/// <item>gib-general soğuk/sıcak render p50/p95 (worker ve süreç içi; Saxon — stylesheet 2.0 fonksiyonları kullanır).</item>
/// </list>
/// Sonuçlar test çıktısına ve <c>XC_MEASURE_OUT</c> (yoksa TEMP) altındaki <c>grid-measurements.md</c>'ye eklenir.
/// </summary>
[Trait("Category", "Measurement")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class GridMeasurementTests(ITestOutputHelper output)
{
    private const int CapSeconds = 60;

    /// <summary>Yüklemde tek N-sınıfı eksen (<c>//</c>) + genel karşılaştırma → .NET'te O(N²).</summary>
    public const string WorstDescendant = "count(//i[@v = //i/@w])";

    /// <summary>Aynı sınıf, ileri eksen: her düğüm için kalan tüm düğümler taranır.</summary>
    public const string WorstFollowing = "count(//i[following::i/@w = @v])";

    /// <summary>Sınırlara dayanan ama doğrusal karışık ifade (3 iç içe yüklem, 16 parantez civarı).</summary>
    public const string NestedPredicates = "count(//d[i[@v != ''][string-length(normalize-space(concat(@v, @w))) > ((((((1))))))]][1])";

    [Theory]
    [InlineData(WorstDescendant)]
    [InlineData(WorstFollowing)]
    [InlineData(NestedPredicates)]
    public async Task WorstAcceptedXPath10_GridInput_Duration(string expression)
    {
        Assert.True(expression.Length <= 512);
        await using var h = await PoolHarness.StartAsync(o =>
        {
            o.RenderTimeoutMs = CapSeconds * 1000;
            o.GcHeapHardLimitMb = 1024;
            o.MaxWorkingSetMb = 0;
            o.RecycleWorkingSetMb = 0;
        }, testOps: false);

        var rows = new List<string>();
        foreach (var items in new[] { 1_000, 4_000, 16_000, MaxItems })
        {
            var xml = GridXml(items, out var nodes);
            foreach (var engine in new[] { WorkerEngines.DotNet, WorkerEngines.Saxon })
            {
                var xslt = $"""<xsl:stylesheet version="{(engine == WorkerEngines.DotNet ? "1.0" : "2.0")}" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"><xsl:output method="text"/><xsl:template match="/"><xsl:value-of select="{System.Security.SecurityElement.Escape(expression)}"/></xsl:template></xsl:stylesheet>""";
                var request = Render(engine, xslt, xml) with
                {
                    Limits = new RequestLimits { DeadlineMs = CapSeconds * 1000, MaxOutputChars = 8_000_000, MaxStylesheetDepth = 256, MaxInputDepth = 64, MaxInputNodes = 200_000 },
                    Timeout = TimeSpan.FromSeconds(CapSeconds),
                };
                var sw = Stopwatch.StartNew();
                var result = await h.Pool.ExecuteAsync(request);
                sw.Stop();
                var outcome = result.Ok ? $"ok ({result.Response.Output})" : $"{result.FailureKind}/{result.ErrorCode}";
                var transformMs = result.Response.Header.Timings?.Transform;
                var row = $"| `{expression}` | {engine} | {items:N0} öğe / {nodes:N0} düğüm / {xml.Length / 1_000_000.0:F1}M kr | {(result.Ok ? Ms(transformMs ?? sw.ElapsedMilliseconds) : $"> {CapSeconds} s (kill)")} | {outcome} |";
                rows.Add(row);
                output.WriteLine(row);
                if (!result.Ok)
                    await h.WaitReadyAsync(seconds: 60);
            }
        }

        Append(rows);
    }

    [Theory]
    [InlineData(XsltEngineKind.DotNet)]
    [InlineData(XsltEngineKind.Saxon)]
    public void Stylesheet6M_Base64_InProcessGridCompileMemory(XsltEngineKind kind)
    {
        var engines = InProcessXsltEngines.CreateDefault(6_000_000);
        var engine = engines.Get(kind);
        var limits = GridLimits();
        var xslt = Base64Stylesheet(6_000_000);
        Assert.InRange(xslt.Length, 5_900_000, 6_000_000);
        _ = engine.Compile(Stylesheet("<x/>"), limits); // motor yükleme maliyeti ölçüme girmesin

        var rows = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var heapBefore = GC.GetTotalMemory(forceFullCollection: true);
            var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            using var proc = Process.GetCurrentProcess();
            var wsBefore = proc.WorkingSet64;
            var sw = Stopwatch.StartNew();

            var compiled = engine.Compile(xslt, limits);
            var compileMs = sw.ElapsedMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
            var retained = GC.GetTotalMemory(forceFullCollection: true) - heapBefore;
            proc.Refresh();
            var peakWsDelta = proc.WorkingSet64 - wsBefore;

            sw.Restart();
            var sb = new StringWriter();
            compiled.Transform("<r/>", sb, limits);
            var transformMs = sw.ElapsedMilliseconds;
            GC.KeepAlive(compiled);

            var row = $"| {kind} | #{i + 1} | {xslt.Length:N0} kr | {compileMs} ms | {Mb(allocated)} | {Mb(retained)} | {Mb(peakWsDelta)} | {transformMs} ms / çıktı {sb.GetStringBuilder().Length:N0} kr |";
            rows.Add(row);
            output.WriteLine(row);
        }

        Append(rows);
    }

    [Fact]
    public async Task GibGeneral_ColdWarm_Latency_Worker()
    {
        var gib = GibGeneral;
        var xml = InvoiceXml;
        var rows = new List<string>();
        // gib-general XSLT 2.0 fonksiyonları (exists() vb.) kullanır → yalnız Saxon.
        foreach (var engine in new[] { WorkerEngines.Saxon })
        {
            var start = Stopwatch.StartNew();
            await using var h = await PoolHarness.StartAsync(testOps: false);
            var startupMs = start.ElapsedMilliseconds;

            var cold = await Timed(() => h.Pool.ExecuteAsync(Render(engine, gib, xml)));
            Assert.True(cold.Result.Ok, cold.Result.Response.Header.Failure?.Message);
            var warm = new List<double>();
            var transform = new List<double>();
            for (var i = 0; i < 100; i++)
            {
                var r = await Timed(() => h.Pool.ExecuteAsync(Render(engine, gib, xml)));
                Assert.True(r.Result.Response.Header.CacheHit);
                warm.Add(r.Ms);
                transform.Add(r.Result.Response.Header.Timings?.Transform ?? 0);
            }

            var row = $"| worker | {engine} | {startupMs} ms (havuz hazır) | {cold.Ms:F1} ms (derleme {cold.Result.Response.Header.Timings?.Compile:F1}) | {P(warm, 50):F1} / {P(warm, 95):F1} ms | {P(transform, 50):F1} / {P(transform, 95):F1} ms |";
            rows.Add(row);
            output.WriteLine(row);
        }

        Append(rows);
    }

    [Fact]
    public async Task GibGeneral_ColdWarm_Latency_InProcess()
    {
        var gib = GibGeneral;
        var xml = InvoiceXml;
        var rows = new List<string>();
        foreach (var kind in new[] { XsltEngineKind.Saxon })
        {
            var start = Stopwatch.StartNew();
            using var host = new InProcessXsltHost(new XsltExecutionGuard(new XsltGridOptions(), TimeProvider.System),
                InProcessXsltEngines.CreateDefault(6_000_000), new NoIsolatedHost(), NullLogger<InProcessXsltHost>.Instance);
            var request = new XsltHostRequest(XsltOperation.Render, kind, gib, xml, GridLimits());

            var cold = await TimedHost(() => host.ExecuteAsync(request));
            var coldTotal = start.ElapsedMilliseconds;
            Assert.True(cold.Result.Diagnostic is null, cold.Result.Diagnostic?.Message);
            var warm = new List<double>();
            for (var i = 0; i < 100; i++)
            {
                var r = await TimedHost(() => host.ExecuteAsync(request));
                Assert.True(r.Result.CacheHit);
                warm.Add(r.Ms);
            }

            var row = $"| süreç içi | {kind} | {coldTotal} ms (motor yükleme dahil) | {cold.Ms:F1} ms (derleme {cold.Result.Timings?.CompileMs}) | {P(warm, 50):F1} / {P(warm, 95):F1} ms | - |";
            rows.Add(row);
            output.WriteLine(row);
        }

        Append(rows);
    }

    // ---- yardımcılar -----------------------------------------------------------------------------

    /// <summary>200k düğüme sığan en çok öğe: zincir 60 &lt;d&gt; (2 okuma/öğe) + öğe başı 5 okuma (start+2 öznitelik, metin, end).</summary>
    private const int MaxItems = (200_000 - 2 - (60 * 2)) / 5;

    /// <summary>Derinlik 62'lik zincirin dibinde <paramref name="items"/> öğe; metin dolgusu ile ~6M karaktere yakın.</summary>
    private static string GridXml(int items, out long nodes)
    {
        const int Chain = 60;
        var pad = new string('x', Math.Max(1, (5_900_000 / MaxItems) - 30));
        var sb = new StringBuilder(items * (pad.Length + 40));
        sb.Append("<r>");
        for (var i = 0; i < Chain; i++)
            sb.Append("<d>");
        for (var i = 0; i < items; i++)
            sb.Append(CultureInfo.InvariantCulture, $"<i v=\"{i % 9973:D5}\" w=\"{(i * 7) % 9967:D5}\">{pad}</i>");
        for (var i = 0; i < Chain; i++)
            sb.Append("</d>");
        sb.Append("</r>");
        nodes = 2 + (Chain * 2) + (items * 5L);
        return sb.ToString();
    }

    /// <summary>Generator benzeri 2.0 stylesheet, gövdesi tek base64 data: URI'li &lt;img&gt;.</summary>
    private static string Base64Stylesheet(int totalChars)
    {
        const string head = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:output method=\"html\" encoding=\"UTF-8\"/><xsl:template match=\"/\"><html><body><img alt=\"logo\" src=\"data:image/png;base64,";
        const string tail = "\"/></body></html></xsl:template></xsl:stylesheet>";
        var payload = totalChars - head.Length - tail.Length - 16;
        var bytes = new byte[payload / 4 * 3];
        new Random(42).NextBytes(bytes);
        return head + Convert.ToBase64String(bytes) + tail;
    }

    private static XsltExecutionLimits GridLimits() => new()
    {
        Deadline = TimeSpan.FromSeconds(60),
        MaxOutputChars = 8_000_000,
        MaxStylesheetDepth = 256,
        MaxInputDepth = 64,
        MaxInputNodes = 200_000,
    };

    private static async Task<(T Result, double Ms)> Timed<T>(Func<Task<T>> run)
    {
        var sw = Stopwatch.StartNew();
        var result = await run();
        return (result, sw.Elapsed.TotalMilliseconds);
    }

    private static Task<(XsltHostResult Result, double Ms)> TimedHost(Func<Task<XsltHostResult>> run) => Timed(run);

    private static double P(List<double> values, int percentile)
    {
        var sorted = values.Order().ToArray();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static string Ms(double ms) => ms >= 1000 ? $"{ms / 1000:F2} s" : $"{ms:F0} ms";

    private static string Mb(long bytes) => $"{bytes / 1048576.0:F1} MB";

    private static void Append(IEnumerable<string> rows)
    {
        var dir = Environment.GetEnvironmentVariable("XC_MEASURE_OUT") is { Length: > 0 } d ? d : Path.GetTempPath();
        lock (typeof(GridMeasurementTests))
            File.AppendAllLines(Path.Combine(dir, "grid-measurements.md"), rows);
    }

    private sealed class NoIsolatedHost : IXsltIsolatedHost
    {
        public bool IsEnabled => false;

        public Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Ölçümde izole host kullanılmaz.");
    }
}
