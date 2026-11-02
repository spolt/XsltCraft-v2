using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt;
using XsltCraft.Infrastructure.Xslt.InProcess;

namespace XsltCraft.Infrastructure.Tests.Xslt;

/// <summary>
/// Süreç içi korumalı host (ADR 008 §7). Kaçak senaryoları gerçek sonsuz döngü YERİNE kontrollü yavaş sahte motorla
/// (<see cref="ControlledEngine"/>) kurulur: thread test sonunda serbest bırakılır, test süreci kirlenmez.
/// Gerçek motor testleri (.NET bit-aynılık, Saxon render, sınırlar) paylaşılan tek motor örneğiyle çalışır.
/// </summary>
public sealed class InProcessXsltHostTests : IDisposable
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";
    private const string Xml = "<r><i>a</i><i>b</i></r>";

    // Saxon Processor kurulumu pahalı (IKVM) — gerçek motor testleri tek örneği paylaşır.
    private static readonly Lazy<InProcessXsltEngines> RealEngines = new(() => InProcessXsltEngines.CreateDefault(6_000_000));

    private readonly ControlledEngine _controlled = new(new DotNetXsltEngine());
    private readonly FakeIsolatedHost _isolated = new();
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        _controlled.Release.Set();
        foreach (var d in _disposables)
            d.Dispose();
    }

    // ------------------------------------------------------------------ kaçak / bozulmuş mod

    [Fact]
    public async Task Timeout_ReturnsTimeout_CountsRunaway()
    {
        var (host, guard) = Create();

        var result = await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(150)));

        Assert.Equal(XsltFailureKind.Timeout, result.Diagnostic!.Kind);
        Assert.Equal(InProcessXsltHost.TimeoutCode, result.Diagnostic.ErrorCode);
        Assert.Equal(XsltExecutionPlace.InProcess, result.Place);
        Assert.Equal(1, guard.RunawayCount);
        Assert.Equal(1, guard.ActiveCount); // thread sürüyor, slotu tutuyor
        Assert.False(guard.IsDegraded);

        _controlled.Release.Set();
        await Eventually(() => guard.RunawayCount == 0 && guard.ActiveCount == 0);
    }

    [Fact]
    public async Task Timeout_CancelsCooperativeEngine_RunawayDrainsWithoutRelease()
    {
        // Limits.Cancellation motora iletilir: işbirlikçi motor guard bırakınca kendiliğinden durur.
        _controlled.HonorCancellation = true;
        var (host, guard) = Create();

        var result = await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(150)));

        Assert.Equal(XsltFailureKind.Timeout, result.Diagnostic!.Kind);
        await Eventually(() => guard.RunawayCount == 0 && guard.ActiveCount == 0);
        Assert.True(_controlled.SawCancellation);
    }

    [Fact]
    public async Task RunawayMax_DegradesToWorker()
    {
        var (host, guard) = Create();
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        Assert.True(guard.IsDegraded);

        var result = await host.ExecuteAsync(Fast());

        Assert.True(result.Diagnostic is null, result.Diagnostic?.Message);
        Assert.Equal(XsltExecutionPlace.Isolated, result.Place);
        Assert.Equal("<worker/>", result.Html);
        var forwarded = Assert.Single(_isolated.Requests);
        Assert.Equal(XsltEngineKind.DotNet, forwarded.Engine);
        Assert.Equal(64, forwarded.Limits.MaxInputDepth);
    }

    [Fact]
    public async Task RunawaysDrained_ReturnsInProcess()
    {
        var (host, guard) = Create();
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        Assert.True(guard.IsDegraded);

        _controlled.Release.Set();
        await Eventually(() => !guard.IsDegraded && guard.ActiveCount == 0);
        var result = await host.ExecuteAsync(Fast());

        Assert.True(result.Diagnostic is null, result.Diagnostic?.Message);
        Assert.Equal(XsltExecutionPlace.InProcess, result.Place);
        Assert.Contains("<p>a</p>", result.Html);
        Assert.Empty(_isolated.Requests);
    }

    [Fact]
    public async Task Degraded_NoWorker_EngineUnavailable()
    {
        _isolated.IsEnabled = false;
        var (host, guard) = Create();
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        Assert.True(guard.IsDegraded);

        var result = await host.ExecuteAsync(Fast());

        Assert.Equal(XsltFailureKind.EngineUnavailable, result.Diagnostic!.Kind);
        Assert.Equal(InProcessXsltHost.DegradedCode, result.Diagnostic.ErrorCode);
        Assert.Empty(_isolated.Requests);
    }

    [Fact]
    public async Task Busy_WhenSlotsFull()
    {
        var (host, guard) = Create(new XsltGridOptions { MaxConcurrent = 1, QueueWaitMs = 50, RunawayMax = 5 });
        var first = host.ExecuteAsync(Slow(TimeSpan.FromSeconds(20)));
        await Eventually(() => guard.ActiveCount == 1);

        var second = await host.ExecuteAsync(Fast());

        Assert.Equal(XsltFailureKind.Busy, second.Diagnostic!.Kind);
        Assert.Equal(InProcessXsltHost.BusyCode, second.Diagnostic.ErrorCode);
        Assert.Equal(0, _controlled.FastTransforms);

        _controlled.Release.Set();
        Assert.True((await first).Diagnostic is null);
        Assert.True((await host.ExecuteAsync(Fast())).Diagnostic is null); // slot geri geldi
    }

    [Fact]
    public async Task ClientCancel_Throws_AndSlotHeldUntilThreadEnds()
    {
        var (host, guard) = Create(new XsltGridOptions { MaxConcurrent = 1, QueueWaitMs = 0, RunawayMax = 5 });
        using var cts = new CancellationTokenSource();
        var running = host.ExecuteAsync(Slow(TimeSpan.FromSeconds(20)), cts.Token);
        await Eventually(() => guard.ActiveCount == 1);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(1, guard.RunawayCount); // iptal slotu kaçıramaz
        Assert.Equal(XsltFailureKind.Busy, (await host.ExecuteAsync(Fast())).Diagnostic!.Kind);
        _controlled.Release.Set();
        await Eventually(() => guard.ActiveCount == 0 && guard.RunawayCount == 0);
    }

    [Fact]
    public async Task CanceledRunaway_ReportsTimeoutToGate()
    {
        // G2 reviewer Y1: istemci iptali işi kaçağa çevirince kesici bunu süreç içi Timeout sayar — yoksa iptal
        // tekrarıyla tek kullanıcı API'yi bozulmuş modda tutabilirdi.
        var (host, guard) = Create(new XsltGridOptions { MaxConcurrent = 4, QueueWaitMs = 0, RunawayMax = 5 });
        var options = new XsltExecutionOptions();
        options.UserGate.InProcessAbuseThreshold = 1;
        var gate = new XsltUserGate(Options.Create(options), TimeProvider.System, NullLogger<XsltUserGate>.Instance);
        var service = new XsltExecutionService(options, new DefaultXsltSafetyScanner(), gate, _isolated, host);
        var user = Guid.NewGuid();

        using var cts = new CancellationTokenSource();
        var running = service.ExecuteAsync(GridRun(slow: true, user), cts.Token);
        await Eventually(() => guard.ActiveCount == 1);
        await cts.CancelAsync();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.IsType<XsltRunawayCanceledException>(ex);
        _controlled.Release.Set();
        await Eventually(() => guard.ActiveCount == 0);

        var next = await service.ExecuteAsync(GridRun(slow: false, user));

        Assert.Equal(XsltFailureKind.UserBusy, next.Diagnostic!.Kind);
        Assert.Equal(XsltUserGate.BlockedCode, next.Diagnostic.ErrorCode);
        Assert.NotNull(next.RetryAfter);
    }

    [Fact]
    public async Task CanceledRunaway_HoldsUserSlotUntilFinished()
    {
        var (host, guard) = Create(new XsltGridOptions { MaxConcurrent = 4, QueueWaitMs = 0, RunawayMax = 5 });
        var options = new XsltExecutionOptions();
        var gate = new XsltUserGate(Options.Create(options), TimeProvider.System, NullLogger<XsltUserGate>.Instance);
        var service = new XsltExecutionService(options, new DefaultXsltSafetyScanner(), gate, _isolated, host);
        var user = Guid.NewGuid();

        using var cts = new CancellationTokenSource();
        var running = service.ExecuteAsync(GridRun(slow: true, user), cts.Token);
        await Eventually(() => guard.ActiveCount == 1);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        // Kaçak sürerken aynı kullanıcının yeni grid isteği: izin hâlâ kaçakta → UserBusy (guard'a ulaşmaz).
        var busy = await service.ExecuteAsync(GridRun(slow: false, user));
        Assert.Equal(XsltFailureKind.UserBusy, busy.Diagnostic!.Kind);
        Assert.Equal(XsltUserGate.BusyCode, busy.Diagnostic.ErrorCode);
        Assert.Equal(0, _controlled.FastTransforms);

        _controlled.Release.Set();
        await Eventually(() => guard.ActiveCount == 0);
        XsltRunResult after = busy;
        await Eventually(() =>
        {
            after = service.ExecuteAsync(GridRun(slow: false, user)).GetAwaiter().GetResult();
            return after.Succeeded;
        });
        Assert.Contains("<p>a</p>", after.Html);
    }

    private static XsltRunRequest GridRun(bool slow, Guid user) => new(XsltRoute.Grid,
        Stylesheet("1.0", slow
            ? ControlledEngine.SlowMarker + "<xsl:template match=\"/\"/>"
            : "<xsl:template match=\"/\"><xsl:for-each select=\"//i\"><p><xsl:value-of select=\".\"/></p></xsl:for-each></xsl:template>"),
        Xml, XsltOperation.Render, user);

    [Fact]
    public async Task DegradedPlace_ReportedToGate()
    {
        // Bozulmuş modda grid worker'a gider; worker Timeout'ları İZOLE eşiğiyle (3) sayılmalı, süreç içi (2) değil.
        _isolated.Respond = r => new XsltHostResult(null, r.Engine, null, false,
            new XsltDiagnostic("süre", 0, 0, XsltFailureKind.Timeout, r.Engine));
        var (host, guard) = Create();
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        Assert.True(guard.IsDegraded);

        var options = new XsltExecutionOptions();
        var gate = new XsltUserGate(Options.Create(options), TimeProvider.System, NullLogger<XsltUserGate>.Instance);
        var service = new XsltExecutionService(options, new DefaultXsltSafetyScanner(), gate, _isolated, host);
        var user = Guid.NewGuid();
        var request = new XsltRunRequest(XsltRoute.Grid, Stylesheet("1.0", "<xsl:template match=\"/\"><b/></xsl:template>"), Xml, XsltOperation.Render, user);

        var results = new List<XsltRunResult>();
        for (var i = 0; i < 3; i++)
            results.Add(await service.ExecuteAsync(request));

        Assert.All(results, r => Assert.Equal(XsltExecutionPlace.Isolated, r.Place));
        Assert.All(results, r => Assert.Equal(XsltFailureKind.Timeout, r.Diagnostic!.Kind)); // 3. istek bloklanmadı
        Assert.Equal(3, _isolated.Requests.Count);
        Assert.All(_isolated.Requests, r => Assert.Equal(user, r.UserId));
    }

    [Fact]
    public async Task IsolatedBlockedUser_CannotReachWorker_ViaDegradedGrid()
    {
        // G1 kalıntısı (G2 #7, kapatıldı): Raw'da İZOLE yerde bloklanan kullanıcı, süreç içi host bozulmuş moddayken grid
        // isteğiyle worker'a ulaşıyordu. Kapı Grid şeridinde iki yerin bloğuna da bakar (fail-closed).
        _isolated.Respond = r => new XsltHostResult(null, r.Engine, null, false,
            new XsltDiagnostic("süre", 0, 0, XsltFailureKind.Timeout, r.Engine));
        var (host, guard) = Create();
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        await host.ExecuteAsync(Slow(TimeSpan.FromMilliseconds(100)));
        Assert.True(guard.IsDegraded);

        var options = new XsltExecutionOptions();
        var gate = new XsltUserGate(Options.Create(options), TimeProvider.System, NullLogger<XsltUserGate>.Instance);
        var service = new XsltExecutionService(options, new DefaultXsltSafetyScanner(), gate, _isolated, host);
        var user = Guid.NewGuid();
        var xslt = Stylesheet("1.0", "<xsl:template match=\"/\"><b/></xsl:template>");

        for (var i = 0; i < options.UserGate.IsolatedAbuseThreshold; i++)
            await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Raw, xslt, Xml, XsltOperation.Render, user));
        var blocked = await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Raw, xslt, Xml, XsltOperation.Render, user));
        Assert.Equal(XsltFailureKind.UserBusy, blocked.Diagnostic!.Kind); // izole yerde blok çalışıyor
        var reachedWorker = _isolated.Requests.Count;

        var grid = await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Grid, xslt, Xml, XsltOperation.Render, user));

        // Grid isteği de (bozulmuş modda fiilî yer izole olabileceğinden) reddedilir, worker'a gitmez.
        Assert.Equal(XsltFailureKind.UserBusy, grid.Diagnostic!.Kind);
        Assert.Equal(reachedWorker, _isolated.Requests.Count);
    }

    // ------------------------------------------------------------------ sınırlar (gerçek motorlar)

    [Theory]
    [InlineData(XsltEngineKind.DotNet)]
    [InlineData(XsltEngineKind.Saxon)]
    public async Task OutputLimit(XsltEngineKind engine)
    {
        var host = CreateReal();
        var xslt = Stylesheet(engine == XsltEngineKind.Saxon ? "2.0" : "1.0",
            "<xsl:template match=\"/\"><xsl:for-each select=\"//i\"><p>0123456789012345678901234567890123456789</p></xsl:for-each></xsl:template>");

        var result = await host.ExecuteAsync(Request(xslt, Xml, engine, GridLimits() with { MaxOutputChars = 40 }));

        Assert.Equal(XsltFailureKind.OutputLimit, result.Diagnostic!.Kind);
        Assert.Null(result.Html);
    }

    [Theory]
    [InlineData(XsltEngineKind.DotNet)]
    [InlineData(XsltEngineKind.Saxon)]
    public async Task InputDepthOver64_Xml(XsltEngineKind engine)
    {
        var host = CreateReal();
        var xslt = Stylesheet(engine == XsltEngineKind.Saxon ? "2.0" : "1.0", "<xsl:template match=\"/\"><ok/></xsl:template>");

        var ok = await host.ExecuteAsync(Request(xslt, Nested(64), engine));
        var over = await host.ExecuteAsync(Request(xslt, Nested(65), engine));

        Assert.True(ok.Diagnostic is null, ok.Diagnostic?.Message);
        Assert.Equal(XsltFailureKind.Xml, over.Diagnostic!.Kind);
        Assert.Contains("64", over.Diagnostic.Message);
    }

    [Fact]
    public async Task Grid_DotNet_MatchesXsltHtmlRenderer()
    {
        // Gerçek grid generator çıktısı (GridV2 golden, version="2.0" ilan eder; Grid=DotNet) + fatura:
        // servis → InProcessXsltHost (.NET, guard thread'i) çıktısı bugünkü renderer'la BİT-AYNI.
        var xslt = await File.ReadAllTextAsync(Fixture("grid-invoice-ready.xslt"));
        var xml = await File.ReadAllTextAsync(Fixture("invoice-basic.xml"));
        var host = CreateReal();
        var options = new XsltExecutionOptions();
        var gate = new XsltUserGate(Options.Create(options), TimeProvider.System, NullLogger<XsltUserGate>.Instance);
        var service = new XsltExecutionService(options, new DefaultXsltSafetyScanner(), gate, _isolated, host);

        var result = await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Grid, xslt, xml, XsltOperation.Render, Guid.NewGuid()));

        Assert.True(result.Succeeded, result.Diagnostic?.Message);
        Assert.Equal(XsltEngineKind.DotNet, result.Engine);
        Assert.Equal(XsltExecutionPlace.InProcess, result.Place);
        Assert.Equal(new XsltHtmlRenderer().Render(xslt, xml).Html, result.Html);
    }

    [Fact]
    public async Task Grid_Saxon_Renders()
    {
        var host = CreateReal();
        var xslt = Stylesheet("2.0",
            "<xsl:template match=\"/\"><ul><xsl:for-each-group select=\"//i\" group-by=\".\">" +
            "<li><xsl:value-of select=\"upper-case(current-grouping-key())\"/>:<xsl:value-of select=\"count(current-group())\"/></li>" +
            "</xsl:for-each-group></ul><s><xsl:value-of select=\"string-join(for $x in //i return $x, '|')\"/></s></xsl:template>");

        var result = await host.ExecuteAsync(Request(xslt, "<r><i>a</i><i>b</i><i>a</i></r>", XsltEngineKind.Saxon));

        Assert.True(result.Diagnostic is null, result.Diagnostic?.Message);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine);
        Assert.Equal(XsltExecutionPlace.InProcess, result.Place);
        Assert.Contains("<li>A:2</li><li>B:1</li>", result.Html);
        Assert.Contains("<s>a|b|a</s>", result.Html);
    }

    [Fact]
    public async Task CacheHit_SecondRequest()
    {
        var (host, _) = Create();
        var xslt = Stylesheet("1.0", "<xsl:template match=\"/\"><p>c</p></xsl:template>");
        var broken = Stylesheet("1.0", "<xsl:template match=\"/\"><xsl:value-of select=\"((\"/></xsl:template>");

        var first = await host.ExecuteAsync(Request(xslt, Xml, XsltEngineKind.DotNet));
        var second = await host.ExecuteAsync(Request(xslt, Xml, XsltEngineKind.DotNet));
        var bad1 = await host.ExecuteAsync(Request(broken, Xml, XsltEngineKind.DotNet));
        var bad2 = await host.ExecuteAsync(Request(broken, Xml, XsltEngineKind.DotNet));

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
        Assert.Equal(0, second.Timings!.CompileMs);
        Assert.Equal(first.Html, second.Html);
        Assert.Equal(XsltFailureKind.Compile, bad1.Diagnostic!.Kind);
        Assert.False(bad2.CacheHit); // başarısız derleme önbelleğe girmez
        Assert.Equal(3, _controlled.Compiles);
        Assert.Equal(1, host.CachedCount);
    }

    [Fact]
    public async Task Validate_CompilesOnly()
    {
        var (host, _) = Create();
        var xslt = Stylesheet("1.0", "<xsl:template match=\"/\"><p/></xsl:template>");

        var result = await host.ExecuteAsync(new XsltHostRequest(XsltOperation.Validate, XsltEngineKind.DotNet, xslt, null, GridLimits()));

        Assert.True(result.Diagnostic is null);
        Assert.Null(result.Html);
        Assert.Equal(0, _controlled.FastTransforms);
    }

    // ------------------------------------------------------------------ ısınma

    [Fact]
    public async Task Warmup_LoadsSaxon()
    {
        var engines = InProcessXsltEngines.CreateDefault(6_000_000);
        var options = new XsltExecutionOptions { Engine = new XsltEngineRoutingOptions { Grid = XsltEngineMode.Auto } };
        var service = new SaxonWarmupService(engines, Options.Create(options), NullLogger<SaxonWarmupService>.Instance);

        var start = service.StartAsync(CancellationToken.None);

        Assert.True(start.IsCompleted); // açılışı bekletmez
        await service.Completion.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(engines.IsSaxonLoaded);
        Assert.Contains("A,B", engines.WarmUpSaxon());
    }

    [Fact]
    public async Task Warmup_SkippedWhenGridDotNet()
    {
        var engines = new InProcessXsltEngines(new DotNetXsltEngine(), () => throw new InvalidOperationException("yüklenmemeli"));
        var service = new SaxonWarmupService(engines, Options.Create(new XsltExecutionOptions()), NullLogger<SaxonWarmupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.Completion;

        Assert.False(engines.IsSaxonLoaded);
    }

    [Fact]
    public async Task SaxonLoadFailure_EngineUnavailable_NotCached()
    {
        var attempts = 0;
        var engines = new InProcessXsltEngines(new DotNetXsltEngine(), () =>
        {
            attempts++;
            throw new FileNotFoundException("IKVM yok");
        });
        var host = Track(new InProcessXsltHost(new XsltExecutionGuard(new XsltGridOptions(), TimeProvider.System),
            engines, _isolated, NullLogger<InProcessXsltHost>.Instance));
        var xslt = Stylesheet("2.0", "<xsl:template match=\"/\"/>");

        var first = await host.ExecuteAsync(Request(xslt, Xml, XsltEngineKind.Saxon));
        await host.ExecuteAsync(Request(xslt, Xml, XsltEngineKind.Saxon));

        Assert.Equal(XsltFailureKind.EngineUnavailable, first.Diagnostic!.Kind);
        Assert.Equal(InProcessXsltEngines.SaxonUnavailableCode, first.Diagnostic.ErrorCode);
        Assert.Equal(2, attempts);
    }

    // ------------------------------------------------------------------ yardımcılar

    private (InProcessXsltHost Host, XsltExecutionGuard Guard) Create(XsltGridOptions? grid = null)
    {
        var guard = new XsltExecutionGuard(grid ?? new XsltGridOptions(), TimeProvider.System);
        var engines = new InProcessXsltEngines(_controlled, () => throw new InvalidOperationException("Saxon bu testte yok"));
        var host = Track(new InProcessXsltHost(guard, engines, _isolated, NullLogger<InProcessXsltHost>.Instance));
        return (host, guard);
    }

    private InProcessXsltHost CreateReal() =>
        Track(new InProcessXsltHost(new XsltExecutionGuard(new XsltGridOptions(), TimeProvider.System),
            RealEngines.Value, _isolated, NullLogger<InProcessXsltHost>.Instance));

    private T Track<T>(T disposable)
        where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    private static XsltExecutionLimits GridLimits(TimeSpan? deadline = null) => new()
    {
        Deadline = deadline ?? TimeSpan.FromSeconds(8),
        MaxOutputChars = 8_000_000,
        MaxStylesheetDepth = 256,
        MaxInputDepth = 64,
        MaxInputNodes = 200_000,
    };

    private static XsltHostRequest Request(string xslt, string xml, XsltEngineKind engine, XsltExecutionLimits? limits = null) =>
        new(XsltOperation.Render, engine, xslt, xml, limits ?? GridLimits()) { UserId = Guid.NewGuid() };

    private static XsltHostRequest Slow(TimeSpan deadline) =>
        Request(Stylesheet("1.0", ControlledEngine.SlowMarker + "<xsl:template match=\"/\"/>"), Xml, XsltEngineKind.DotNet, GridLimits(deadline));

    private static XsltHostRequest Fast() =>
        Request(Stylesheet("1.0", "<xsl:template match=\"/\"><xsl:for-each select=\"//i\"><p><xsl:value-of select=\".\"/></p></xsl:for-each></xsl:template>"),
            Xml, XsltEngineKind.DotNet);

    private static string Stylesheet(string version, string body) =>
        $"<xsl:stylesheet version=\"{version}\" xmlns:xsl=\"{XslNs}\">{body}</xsl:stylesheet>";

    private static string Nested(int depth) =>
        string.Concat(Enumerable.Repeat("<d>", depth)) + string.Concat(Enumerable.Repeat("</d>", depth));

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Koşul 10 sn içinde sağlanmadı.");
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Test seam: <see cref="SlowMarker"/> içeren stylesheet'in dönüşümü <see cref="Release"/>'e dek bloklanır
    /// (iptali yok sayarak kaçak taklidi; <see cref="HonorCancellation"/> ile işbirlikçi). Diğerleri gerçek .NET motoruna.
    /// Güvenlik payı: blok en fazla 30 sn (test süreci asla asılı kalmaz).
    /// </summary>
    private sealed class ControlledEngine(IXsltEngine inner) : IXsltEngine
    {
        public const string SlowMarker = "<!--slow-->";

        private int _compiles;
        private int _fastTransforms;

        public ManualResetEventSlim Release { get; } = new(false);

        public bool HonorCancellation { get; set; }

        public bool SawCancellation { get; private set; }

        public int Compiles => Volatile.Read(ref _compiles);

        public int FastTransforms => Volatile.Read(ref _fastTransforms);

        public XsltEngineKind Kind => inner.Kind;

        public ICompiledXslt Compile(string xslt, XsltExecutionLimits limits)
        {
            Interlocked.Increment(ref _compiles);
            return xslt.Contains(SlowMarker, StringComparison.Ordinal) ? new SlowCompiled(this) : new Counting(this, inner.Compile(xslt, limits));
        }

        private sealed class Counting(ControlledEngine owner, ICompiledXslt compiled) : ICompiledXslt
        {
            public XsltEngineKind Kind => compiled.Kind;

            public XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits)
            {
                Interlocked.Increment(ref owner._fastTransforms);
                return compiled.Transform(xml, output, limits);
            }
        }

        private sealed class SlowCompiled(ControlledEngine owner) : ICompiledXslt
        {
            public XsltEngineKind Kind => owner.Kind;

            public XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits)
            {
                var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                while (!owner.Release.Wait(10) && DateTime.UtcNow < until)
                {
                    if (owner.HonorCancellation && limits.Cancellation.IsCancellationRequested)
                    {
                        owner.SawCancellation = true;
                        throw new XsltProcessingException(new XsltDiagnostic("iptal", 0, 0, XsltFailureKind.Timeout));
                    }
                }

                output.Write("<p>slow</p>");
                return new XsltTransformTimings(0, 0, 0, 0);
            }
        }
    }

    private sealed class FakeIsolatedHost : IXsltIsolatedHost
    {
        public bool IsEnabled { get; set; } = true;

        public Func<XsltHostRequest, XsltHostResult>? Respond { get; set; }

        public List<XsltHostRequest> Requests { get; } = [];

        public Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default)
        {
            lock (Requests)
                Requests.Add(request);
            return Task.FromResult(Respond?.Invoke(request) ?? new XsltHostResult("<worker/>", request.Engine, null, false, null));
        }
    }
}
