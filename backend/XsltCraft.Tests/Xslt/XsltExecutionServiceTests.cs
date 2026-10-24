using System.Diagnostics.CodeAnalysis;

using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Tests.Xslt;

/// <summary>
/// XsltExecutionService rota yönlendirmesi: boyut → güvenlik (Execute) → sürüm/motor → kapı → host.
/// Host ve kapı sahtedir; tarayıcı çoğu testte gerçektir (satır/kolon ve kod gerçek bulgudan gelir).
/// </summary>
public sealed class XsltExecutionServiceTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";
    private const string Xml = "<r><i>a</i></r>";
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeHost _isolated = new() { IsEnabled = true };
    private readonly FakeHost _inProcess = new();
    private readonly FakeGate _gate = new();

    private static string Stylesheet(string version, string body = "<xsl:template match=\"/\"><p><xsl:value-of select=\"//i\"/></p></xsl:template>") =>
        $"<xsl:stylesheet version=\"{version}\" xmlns:xsl=\"{XslNs}\">{body}</xsl:stylesheet>";

    private XsltExecutionService Create(XsltExecutionOptions? options = null, IXsltSafetyScanner? scanner = null) =>
        new(options ?? new XsltExecutionOptions(), scanner ?? new DefaultXsltSafetyScanner(), _gate, _isolated, _inProcess);

    private static XsltRunRequest Raw(string xslt, string? xml = Xml) => new(XsltRoute.Raw, xslt, xml, XsltOperation.Render, User);

    // ------------------------------------------------------------------ Raw

    [Fact]
    public async Task Raw_V1_DotNetInWorker()
    {
        var result = await Create().ExecuteAsync(Raw(Stylesheet("1.0")));

        Assert.True(result.Succeeded);
        Assert.Equal(XsltExecutionPlace.Isolated, result.Place);
        Assert.Equal(XsltEngineKind.DotNet, result.Engine);
        var sent = Assert.Single(_isolated.Requests);
        Assert.Equal(XsltEngineKind.DotNet, sent.Engine);
        Assert.Equal(XsltOperation.Render, sent.Op);
        Assert.Equal(Xml, sent.Xml);
        Assert.Equal(TimeSpan.FromSeconds(10), sent.Limits.Deadline);
        Assert.Empty(_inProcess.Requests);
        Assert.Equal((XsltGateLane.Raw, XsltExecutionPlace.Isolated), Assert.Single(_gate.Acquired));
    }

    [Fact]
    public async Task Raw_V2_Saxon()
    {
        var result = await Create().ExecuteAsync(Raw(Stylesheet("2.0")));

        Assert.True(result.Succeeded);
        Assert.Equal(XsltEngineKind.Saxon, Assert.Single(_isolated.Requests).Engine);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine);
        Assert.Equal("<html>ok</html>", result.Html);
    }

    [Fact]
    public async Task Raw_V3_Unsupported()
    {
        var result = await Create().ExecuteAsync(Raw(Stylesheet("3.0")));

        Assert.False(result.Succeeded);
        Assert.Equal(XsltFailureKind.Unsupported, result.Diagnostic!.Kind);
        Assert.Equal("VersionAbove20", result.Diagnostic.ErrorCode);
        AssertNothingExecuted();
    }

    [Fact]
    public async Task Raw_V3_Unsupported_EvenIfScannerMisses()
    {
        var result = await Create(scanner: new FakeScanner()).ExecuteAsync(Raw(Stylesheet("3.0")));

        Assert.Equal(XsltFailureKind.Unsupported, result.Diagnostic!.Kind);
        Assert.Equal("VersionAbove20", result.Diagnostic.ErrorCode);
        AssertNothingExecuted();
    }

    [Fact]
    public async Task Threat_SecurityWithLine()
    {
        var xslt = $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
              <xsl:template match="/">
                <p><xsl:value-of select="document('file:///etc/passwd')"/></p>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var result = await Create().ExecuteAsync(Raw(xslt));

        Assert.Equal(XsltFailureKind.Security, result.Diagnostic!.Kind);
        Assert.Equal(3, result.Diagnostic.Line);
        Assert.True(result.Diagnostic.Column > 0);
        Assert.False(string.IsNullOrEmpty(result.Diagnostic.ErrorCode));
        Assert.Null(result.Engine);
        AssertNothingExecuted();
    }

    [Fact]
    public async Task Scanner_IsCalledInExecuteMode_UnsupportedFindingMapped()
    {
        var scanner = new FakeScanner
        {
            Report = new XsltSafetyReport([new XsltSafetyFinding("Iterate", XsltSafetySeverity.Unsupported, "xsl:iterate desteklenmiyor.", 4, 7)]),
        };

        var result = await Create(scanner: scanner).ExecuteAsync(Raw(Stylesheet("2.0")));

        Assert.Equal(XsltSafetyMode.Execute, Assert.Single(scanner.Modes));
        Assert.Equal(new XsltDiagnostic("xsl:iterate desteklenmiyor.", 4, 7, XsltFailureKind.Unsupported, null, "Iterate"), result.Diagnostic);
        AssertNothingExecuted();
    }

    // ------------------------------------------------------------------ boyut

    private static XsltExecutionOptions SmallRawLimits() => new()
    {
        Limits = new XsltRouteLimitsOptions
        {
            Raw = new XsltRouteLimits { MaxXsltChars = 400, MaxXsltBytes = 500, MaxXmlChars = 100, MaxXmlBytes = 120, TimeoutMs = 1000 },
        },
    };

    [Fact]
    public async Task Oversize_Rejected()
    {
        var sut = Create(SmallRawLimits());

        var bigXslt = await sut.ExecuteAsync(Raw(Stylesheet("1.0", $"<xsl:template match=\"/\">{new string('x', 400)}</xsl:template>")));
        var bigXml = await sut.ExecuteAsync(Raw(Stylesheet("1.0"), $"<r>{new string('x', 100)}</r>"));

        Assert.Equal(XsltFailureKind.LimitExceeded, bigXslt.Diagnostic!.Kind);
        Assert.Equal("XsltTooLarge", bigXslt.Diagnostic.ErrorCode);
        Assert.Contains("400 karakter", bigXslt.Diagnostic.Message);
        Assert.Equal(XsltFailureKind.LimitExceeded, bigXml.Diagnostic!.Kind);
        Assert.Equal("XmlTooLarge", bigXml.Diagnostic.ErrorCode);
        AssertNothingExecuted();
    }

    [Fact]
    public async Task OversizeUtf8Bytes_Rejected()
    {
        var sut = Create(SmallRawLimits());

        // 80 '€' = 80 karakter (< 100) ama 240 UTF-8 bayt (> 120).
        var result = await sut.ExecuteAsync(Raw(Stylesheet("1.0"), $"<r>{new string('€', 80)}</r>"));
        // Aynı karakter sayısında ASCII sınırın altında kalır.
        var ascii = await sut.ExecuteAsync(Raw(Stylesheet("1.0"), $"<r>{new string('e', 80)}</r>"));

        Assert.Equal(XsltFailureKind.LimitExceeded, result.Diagnostic!.Kind);
        Assert.Equal("XmlTooLarge", result.Diagnostic.ErrorCode);
        Assert.Contains("UTF-8", result.Diagnostic.Message);
        Assert.True(ascii.Succeeded);
        Assert.Single(_isolated.Requests);
    }

    [Fact]
    public void DefaultLimits_AreByteBudgetCompatible()
    {
        var options = new XsltExecutionOptions();

        Assert.Empty(options.Validate());
        var raw = options.Limits.Raw;
        Assert.Equal(2_000_000, raw.MaxXsltChars);
        Assert.Equal(5_000_000, raw.MaxXmlChars);
        Assert.Equal(1_000_000, options.Limits.Validate.MaxXsltChars);
        Assert.Equal(5 * 1024 * 1024, options.Limits.Grid.MaxXmlBytes);
        Assert.True(raw.MaxXsltBytes + raw.MaxXmlBytes <= options.Limits.IsolatedMaxRequestBytes);
    }

    [Fact]
    public void Options_OverBudget_Rejected()
    {
        var options = new XsltExecutionOptions();
        options.Limits.Raw.MaxXmlBytes = 10 * 1024 * 1024;

        Assert.Contains(options.Validate(), e => e.Contains("Limits:Raw", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => Create(options));
    }

    // ------------------------------------------------------------------ Grid / UserTemplate

    [Fact]
    public async Task Grid_DefaultDotNet_InProcess()
    {
        // Generator çıktısı version="2.0" ilan eder; Grid modu DotNet → 2.0 olsa da .NET.
        var request = new XsltRunRequest(XsltRoute.Grid, Stylesheet("2.0"), Xml, XsltOperation.Render, User);

        var result = await Create().ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(XsltExecutionPlace.InProcess, result.Place);
        var sent = Assert.Single(_inProcess.Requests);
        Assert.Equal(XsltEngineKind.DotNet, sent.Engine);
        Assert.Equal(64, sent.Limits.MaxInputDepth);
        Assert.Equal(200_000, sent.Limits.MaxInputNodes);
        Assert.Equal(TimeSpan.FromSeconds(8), sent.Limits.Deadline);
        Assert.Empty(_isolated.Requests);
        Assert.Equal((XsltGateLane.Grid, XsltExecutionPlace.InProcess), Assert.Single(_gate.Acquired));
    }

    [Fact]
    public async Task Grid_AutoMode_SelectsSaxon()
    {
        var options = new XsltExecutionOptions { Engine = new XsltEngineRoutingOptions { Grid = XsltEngineMode.Auto } };
        var request = new XsltRunRequest(XsltRoute.Grid, Stylesheet("2.0"), Xml, XsltOperation.Render, User);

        await Create(options).ExecuteAsync(request);

        Assert.Equal(XsltEngineKind.Saxon, Assert.Single(_inProcess.Requests).Engine);
    }

    [Fact]
    public async Task UserTemplate_GoesToWorker()
    {
        var request = new XsltRunRequest(XsltRoute.UserTemplate, Stylesheet("2.0"), Xml, XsltOperation.Render, User);

        var result = await Create().ExecuteAsync(request);

        Assert.Equal(XsltExecutionPlace.Isolated, result.Place);
        var sent = Assert.Single(_isolated.Requests);
        Assert.Equal(XsltEngineKind.DotNet, sent.Engine); // Grid modunu izler (DotNet)
        Assert.Equal(TimeSpan.FromSeconds(8), sent.Limits.Deadline);
        Assert.Empty(_inProcess.Requests);
        Assert.Equal((XsltGateLane.UserTemplate, XsltExecutionPlace.Isolated), Assert.Single(_gate.Acquired));
    }

    [Fact]
    public async Task ScriptBlockGrid_GoesToWorker()
    {
        var request = new XsltRunRequest(XsltRoute.Grid, Stylesheet("2.0"), Xml, XsltOperation.Render, User) { IsUserAuthoredGrid = true };

        var result = await Create().ExecuteAsync(request);

        Assert.Equal(XsltExecutionPlace.Isolated, result.Place);
        Assert.Single(_isolated.Requests);
        Assert.Empty(_inProcess.Requests);
        Assert.Equal((XsltGateLane.Grid, XsltExecutionPlace.Isolated), Assert.Single(_gate.Acquired));
    }

    [Fact]
    public async Task Grid_InterimInProcessHost_MatchesXsltHtmlRenderer()
    {
        var xslt = Stylesheet("2.0", "<xsl:output method=\"html\"/><xsl:template match=\"/\"><div><xsl:value-of select=\"//i\"/></div></xsl:template>");
        var sut = new XsltExecutionService(new XsltExecutionOptions(), new DefaultXsltSafetyScanner(), _gate, _isolated, new DotNetInProcessXsltHost());

        var result = await sut.ExecuteAsync(new XsltRunRequest(XsltRoute.Grid, xslt, Xml, XsltOperation.Render, User));

        Assert.True(result.Succeeded, result.Diagnostic?.Message);
        Assert.Equal(new XsltHtmlRenderer().Render(xslt, Xml).Html, result.Html);
        Assert.Equal(XsltEngineKind.DotNet, result.Engine);
    }

    // ------------------------------------------------------------------ worker kapalı

    [Fact]
    public async Task WorkerDisabled_Fallbacks()
    {
        _isolated.IsEnabled = false;
        var sut = Create();

        var v1 = await sut.ExecuteAsync(Raw(Stylesheet("1.0")));
        var v2 = await sut.ExecuteAsync(Raw(Stylesheet("2.0")));
        var userTemplate = await sut.ExecuteAsync(new XsltRunRequest(XsltRoute.UserTemplate, Stylesheet("2.0"), Xml, XsltOperation.Render, User));
        var scriptGrid = await sut.ExecuteAsync(new XsltRunRequest(XsltRoute.Grid, Stylesheet("2.0"), Xml, XsltOperation.Render, User) { IsUserAuthoredGrid = true });

        Assert.True(v1.Succeeded);
        Assert.Equal(XsltExecutionPlace.InProcess, v1.Place);
        Assert.Equal(XsltFailureKind.EngineUnavailable, v2.Diagnostic!.Kind);
        Assert.Equal(XsltEngineKind.Saxon, v2.Engine);
        Assert.Equal(XsltExecutionPlace.InProcess, userTemplate.Place); // Grid modu DotNet → 1.0 motoru
        Assert.Equal(XsltFailureKind.EngineUnavailable, scriptGrid.Diagnostic!.Kind);

        Assert.Empty(_isolated.Requests);
        Assert.Equal([XsltEngineKind.DotNet, XsltEngineKind.DotNet], _inProcess.Requests.Select(r => r.Engine));
        Assert.Equal(
            [(XsltGateLane.Raw, XsltExecutionPlace.InProcess), (XsltGateLane.UserTemplate, XsltExecutionPlace.InProcess)],
            _gate.Acquired);
    }

    // ------------------------------------------------------------------ Validate

    [Fact]
    public async Task Validate_UsesValidateLane_NoXml_NoHtml()
    {
        var request = new XsltRunRequest(XsltRoute.Raw, Stylesheet("2.0"), "<ignored/>", XsltOperation.Validate, User);

        var result = await Create().ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.Null(result.Html);
        var sent = Assert.Single(_isolated.Requests);
        Assert.Equal(XsltOperation.Validate, sent.Op);
        Assert.Null(sent.Xml);
        Assert.Equal(TimeSpan.FromSeconds(5), sent.Limits.Deadline);
        Assert.Equal((XsltGateLane.Validate, XsltExecutionPlace.Isolated), Assert.Single(_gate.Acquired));
    }

    [Fact]
    public async Task Validate_OverOneMillionChars_Rejected()
    {
        var xslt = Stylesheet("1.0", $"<xsl:template match=\"/\">{new string('x', 1_000_000)}</xsl:template>");

        var result = await Create().ExecuteAsync(new XsltRunRequest(XsltRoute.Raw, xslt, null, XsltOperation.Validate, User));

        Assert.Equal(XsltFailureKind.LimitExceeded, result.Diagnostic!.Kind);
        AssertNothingExecuted();
    }

    // ------------------------------------------------------------------ kapı

    [Fact]
    public async Task GateDenied_UserBusy_WithRetryAfter_NoHostCall()
    {
        _gate.Deny = new XsltGateDenial("blok", "UserBlocked", TimeSpan.FromMinutes(7));

        var result = await Create().ExecuteAsync(Raw(Stylesheet("1.0")));

        Assert.Equal(XsltFailureKind.UserBusy, result.Diagnostic!.Kind);
        Assert.Equal("UserBlocked", result.Diagnostic.ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(7), result.RetryAfter);
        Assert.Empty(_isolated.Requests);
    }

    [Fact]
    public async Task GateReleased_OnException()
    {
        _isolated.Throw = new InvalidOperationException("host bozuk");
        var sut = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(Raw(Stylesheet("1.0"))));

        Assert.Equal(1, _gate.Disposed);
        Assert.Empty(_gate.Reported);
    }

    [Fact]
    public async Task GateReleased_AndOutcomeReported_OnFailureResultAndProcessingException()
    {
        var sut = Create();
        _isolated.Result = r => XsltHostResult.Failed(new XsltDiagnostic("süre", 0, 0, XsltFailureKind.Timeout, r.Engine), r.Engine);
        var timeout = await sut.ExecuteAsync(Raw(Stylesheet("1.0")));

        _isolated.Result = null;
        _isolated.Throw = new XsltProcessingException(new XsltDiagnostic("derleme", 2, 3, XsltFailureKind.Compile));
        var compile = await sut.ExecuteAsync(Raw(Stylesheet("1.0")));

        Assert.Equal(XsltFailureKind.Timeout, timeout.Diagnostic!.Kind);
        Assert.Equal(XsltFailureKind.Compile, compile.Diagnostic!.Kind);
        Assert.Equal(2, _gate.Disposed);
        Assert.Equal([XsltFailureKind.Timeout, XsltFailureKind.Compile], _gate.Reported);
    }

    [Fact]
    public async Task Cancellation_FlowsToHostAndLimits()
    {
        using var cts = new CancellationTokenSource();

        await Create().ExecuteAsync(Raw(Stylesheet("1.0")), cts.Token);

        Assert.Equal(cts.Token, Assert.Single(_isolated.Requests).Limits.Cancellation);
        Assert.Equal(cts.Token, _isolated.Tokens.Single());
    }

    [Fact]
    public async Task ThemeRoute_NotRouted()
    {
        var request = new XsltRunRequest(XsltRoute.Theme, Stylesheet("1.0"), Xml, XsltOperation.Render, User);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Create().ExecuteAsync(request));
    }

    private void AssertNothingExecuted()
    {
        Assert.Empty(_isolated.Requests);
        Assert.Empty(_inProcess.Requests);
        Assert.Empty(_gate.Acquired);
    }

    // ------------------------------------------------------------------ sahteler

    private sealed class FakeHost : IXsltIsolatedHost, IXsltInProcessHost
    {
        public bool IsEnabled { get; set; }

        public Func<XsltHostRequest, XsltHostResult>? Result { get; set; }

        public Exception? Throw { get; set; }

        public List<XsltHostRequest> Requests { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Tokens.Add(cancellationToken);
            if (Throw is not null)
                throw Throw;
            return Task.FromResult(Result?.Invoke(request)
                ?? new XsltHostResult(request.Op == XsltOperation.Render ? "<html>ok</html>" : null, request.Engine, new XsltTransformTimings(1, 2, 3, 4), false, null));
        }
    }

    private sealed class FakeScanner : IXsltSafetyScanner
    {
        public XsltSafetyReport Report { get; init; } = XsltSafetyReport.Clean;

        public List<XsltSafetyMode> Modes { get; } = [];

        public XsltSafetyReport Scan(string? xslt, XsltSafetyMode mode)
        {
            Modes.Add(mode);
            return Report;
        }
    }

    private sealed class FakeGate : IXsltUserGate
    {
        public XsltGateDenial? Deny { get; set; }

        public List<(XsltGateLane, XsltExecutionPlace)> Acquired { get; } = [];

        public List<XsltFailureKind?> Reported { get; } = [];

        public int Disposed { get; private set; }

        public bool TryAcquire(
            Guid userId, XsltGateLane lane, XsltExecutionPlace place,
            [NotNullWhen(true)] out IXsltGateLease? lease, [NotNullWhen(false)] out XsltGateDenial? denial)
        {
            if (Deny is not null)
            {
                lease = null;
                denial = Deny;
                return false;
            }

            Acquired.Add((lane, place));
            lease = new FakeLease(this);
            denial = null;
            return true;
        }

        private sealed class FakeLease(FakeGate gate) : IXsltGateLease
        {
            public void ReportOutcome(XsltFailureKind? failure) => gate.Reported.Add(failure);

            public void Dispose() => gate.Disposed++;
        }
    }
}
