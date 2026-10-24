using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Tests.Xslt;

public sealed class WorkerXsltHostTests
{
    private const string Xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"/>";
    private const string Xml = "<r/>";

    private readonly FakePool _pool = new();

    private WorkerXsltHost Create(bool enabled = true) =>
        new(_pool, Options.Create(new XsltWorkerOptions { Enabled = enabled }));

    private static XsltHostRequest Request(XsltOperation op, XsltEngineKind engine, TimeSpan? deadline = null) =>
        new(op, engine, Xslt, op == XsltOperation.Render ? Xml : null, XsltExecutionLimits.Default with
        {
            Deadline = deadline ?? TimeSpan.FromSeconds(10),
            MaxOutputChars = 1234,
            MaxStylesheetDepth = 77,
            MaxInputDepth = 64,
            MaxInputNodes = 200_000,
        });

    [Fact]
    public void FailureKindParity()
    {
        var contractNames = typeof(WorkerFailureKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Enum.GetNames<XsltFailureKind>().Order(StringComparer.Ordinal), contractNames);

        // Sabit adı = değeri (WorkerFailureKinds.Timeout == "Timeout").
        foreach (var field in typeof(WorkerFailureKinds).GetFields(BindingFlags.Public | BindingFlags.Static))
            Assert.Equal(field.Name, field.GetRawConstantValue());
    }

    [Fact]
    public void MapKind_AllContractKinds_RoundTrip()
    {
        foreach (var kind in Enum.GetValues<XsltFailureKind>())
            Assert.Equal(kind, WorkerXsltHost.MapKind(kind.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("timeout")]       // büyük/küçük harf farkı kabul edilmez
    [InlineData("3")]             // Enum.TryParse sayıyı kabul ederdi
    [InlineData("SomethingNew")]  // sürüm kayması
    [InlineData("Timeout,Xml")]   // bayrak sözdizimi
    public void MapKind_Unknown_EngineInternal(string? wire)
    {
        Assert.Equal(XsltFailureKind.EngineInternal, WorkerXsltHost.MapKind(wire));
    }

    [Fact]
    public async Task Render_MapsRequest()
    {
        await Create().ExecuteAsync(Request(XsltOperation.Render, XsltEngineKind.Saxon));

        var sent = Assert.Single(_pool.Requests);
        Assert.Equal(WorkerOps.Render, sent.Op);
        Assert.Equal(WorkerEngines.Saxon, sent.Engine);
        Assert.Equal([Xslt, Xml], sent.Bodies);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Xslt))), sent.XsltSha256);
        Assert.Equal(TimeSpan.FromSeconds(10), sent.Timeout);
        Assert.Equal(10_000 - (int)WorkerXsltHost.CooperativeMargin.TotalMilliseconds, sent.Limits!.DeadlineMs);
        Assert.Equal(1234, sent.Limits.MaxOutputChars);
        Assert.Equal(77, sent.Limits.MaxStylesheetDepth);
        Assert.Equal(64, sent.Limits.MaxInputDepth);
        Assert.Equal(200_000, sent.Limits.MaxInputNodes);
    }

    [Fact]
    public async Task Validate_SendsOnlyXslt_DotNetEngine()
    {
        await Create().ExecuteAsync(Request(XsltOperation.Validate, XsltEngineKind.DotNet, TimeSpan.FromMilliseconds(300)));

        var sent = Assert.Single(_pool.Requests);
        Assert.Equal(WorkerOps.Validate, sent.Op);
        Assert.Equal(WorkerEngines.DotNet, sent.Engine);
        Assert.Equal([Xslt], sent.Bodies);
        Assert.Equal(300, sent.Limits!.DeadlineMs); // pay süreden büyükse sert süre kullanılır
    }

    [Fact]
    public async Task Success_MapsHtmlTimingsEngineCacheHit()
    {
        _pool.Next = new ResponseHeader
        {
            Ok = true,
            Engine = WorkerEngines.Saxon,
            CacheHit = true,
            Timings = new ResponseTimings { Parse = 1.4, Compile = 2.6, Transform = 3, Serialize = double.NaN },
        };
        _pool.NextOutput = "<html/>";

        var result = await Create().ExecuteAsync(Request(XsltOperation.Render, XsltEngineKind.Saxon));

        Assert.Null(result.Diagnostic);
        Assert.Equal("<html/>", result.Html);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine);
        Assert.True(result.CacheHit);
        Assert.Equal(new XsltTransformTimings(1, 3, 3, 0), result.Timings);
    }

    [Fact]
    public async Task Validate_Success_NoHtml()
    {
        _pool.Next = new ResponseHeader { Ok = true, Engine = WorkerEngines.DotNet };

        var result = await Create().ExecuteAsync(Request(XsltOperation.Validate, XsltEngineKind.DotNet));

        Assert.Null(result.Diagnostic);
        Assert.Null(result.Html);
    }

    [Fact]
    public async Task Failure_MapsDiagnostic()
    {
        _pool.Next = new ResponseHeader
        {
            Ok = false,
            Failure = new ResponseFailure { Kind = WorkerFailureKinds.Compile, Message = "XPST0003", Line = 4, Column = 9, ErrorCode = "XPST0003" },
        };

        var result = await Create().ExecuteAsync(Request(XsltOperation.Render, XsltEngineKind.Saxon));

        Assert.Equal(new XsltDiagnostic("XPST0003", 4, 9, XsltFailureKind.Compile, XsltEngineKind.Saxon, "XPST0003"), result.Diagnostic);
        Assert.Null(result.Html);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine); // yanıtta motor yoksa istenen
    }

    [Fact]
    public async Task PoolSynthesizedFailure_UnknownKind_EngineInternal_DefaultMessage()
    {
        _pool.Next = new ResponseHeader { Ok = false, Failure = new ResponseFailure { Kind = "Brand-new", Message = " " } };

        var result = await Create().ExecuteAsync(Request(XsltOperation.Render, XsltEngineKind.DotNet));

        Assert.Equal(XsltFailureKind.EngineInternal, result.Diagnostic!.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic.Message));
        Assert.Equal(0, result.Diagnostic.Line);
    }

    [Fact]
    public void IsEnabled_FollowsWorkerOptions()
    {
        Assert.True(Create(enabled: true).IsEnabled);
        Assert.False(Create(enabled: false).IsEnabled);
    }

    [Fact]
    public void DefaultIsolatedBudget_FitsWorkerRequestFrame()
    {
        var limits = new XsltExecutionOptions().Limits;

        Assert.True(limits.IsolatedMaxRequestBytes + FrameLimits.MaxHeaderBytes < FrameLimits.MaxRequestFrameBytes);
        foreach (var route in new[] { limits.Raw, limits.Validate, limits.UserTemplate, limits.Grid })
            Assert.True((long)route.MaxXsltBytes + route.MaxXmlBytes <= limits.IsolatedMaxRequestBytes);
    }

    private sealed class FakePool : IXsltWorkerPool
    {
        public List<XsltWorkerRequest> Requests { get; } = [];

        public ResponseHeader Next { get; set; } = new() { Ok = true };

        public string? NextOutput { get; set; } = "<html>ok</html>";

        public Task<XsltWorkerPoolResult> ExecuteAsync(XsltWorkerRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new XsltWorkerPoolResult(new WorkerResponse(Next with { Id = "1" }, Next.Ok ? NextOutput : null), 42));
        }
    }
}
