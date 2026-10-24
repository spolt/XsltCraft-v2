using System.Diagnostics;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// <b>GEÇİCİ</b> <see cref="IXsltInProcessHost"/>: istek thread'inde <see cref="DotNetXsltEngine"/>
/// (bugünkü grid önizlemesinin <c>IXsltHtmlRenderer</c> yoluyla bit-aynı çıktı). Guard'sızdır —
/// koruma yalnız işbirlikçi süre/iptal ve çıktı/girdi sınırlarıdır; risk profili bugünkü süreç içi
/// grid'le aynıdır. T2.4b'de <c>InProcessXsltHost</c> (guard thread, semafor, bozulmuş mod, Saxon)
/// ile değiştirilir. Saxon istenirse <see cref="XsltFailureKind.EngineUnavailable"/>.
/// </summary>
public sealed class DotNetInProcessXsltHost : IXsltInProcessHost
{
    private readonly DotNetXsltEngine _engine = new();

    public Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Engine != XsltEngineKind.DotNet)
        {
            return Task.FromResult(XsltHostResult.Failed(
                new XsltDiagnostic("Süreç içi XSLT 2.0 motoru henüz etkin değil.", 0, 0,
                    XsltFailureKind.EngineUnavailable, request.Engine, "InProcessSaxonUnavailable"),
                request.Engine));
        }

        try
        {
            return Task.FromResult(Run(request));
        }
        catch (XsltProcessingException ex)
        {
            return Task.FromResult(XsltHostResult.Failed(ex.Diagnostic, XsltEngineKind.DotNet));
        }
    }

    private XsltHostResult Run(XsltHostRequest request)
    {
        var sw = Stopwatch.StartNew();
        var compiled = _engine.Compile(request.Xslt, request.Limits);
        var compileMs = sw.ElapsedMilliseconds;

        if (request.Op == XsltOperation.Validate)
            return new XsltHostResult(null, XsltEngineKind.DotNet, new XsltTransformTimings(0, compileMs, 0, 0), false, null);

        request.Limits.Cancellation.ThrowIfCancellationRequested();
        using var output = new StringWriter();
        var timings = compiled.Transform(request.Xml ?? "", output, request.Limits) with { CompileMs = compileMs };
        return new XsltHostResult(output.ToString(), XsltEngineKind.DotNet, timings, false, null);
    }
}
