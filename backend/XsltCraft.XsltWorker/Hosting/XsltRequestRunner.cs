using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// <c>render</c> / <c>validate</c> yürütücüsü (motor thread'inde). Sıra:
/// <list type="number">
/// <item>Motor çözümü (<c>saxon</c> / <c>dotnet</c>) ve gövde denetimi.</item>
/// <item>SHA-256 doğrulaması: istekteki <c>xsltSha256</c> gövdenin kendi özetiyle eşleşmeli (önbellek anahtarı
///   worker'ın hesapladığıdır; uyuşmazlık → <c>EngineInternal</c>, önbelleğe yazılmaz).</item>
/// <item><see cref="XsltSafety"/> Execute taraması — derinlemesine savunma (Api de tarar). <paramref name="screen"/>
///   <c>false</c> yalnız <c>--test-ops</c>'un <c>render-unscreened</c> op'unda (havuz ASLA geçmez).</item>
/// <item>Derleme (LRU önbellek) → <c>validate</c> burada biter; <c>render</c> dönüşüm + gövde.</item>
/// </list>
/// </summary>
internal sealed class XsltRequestRunner
{
    private readonly WorkerEngineSet _engines;
    private readonly CompileCache _cache;

    public XsltRequestRunner(WorkerEngineSet engines, CompileCache cache)
    {
        _engines = engines;
        _cache = cache;
    }

    public HandlerResult Run(RequestHeader request, IReadOnlyList<byte[]> bodies, bool render, bool screen)
    {
        // İşbirlikçi süre isteğin başında başlar: tarama + derleme + dönüşüm toplamı.
        var limits = RequestLimitsMapper.ToExecutionLimits(request.Limits);
        using var deadline = new CooperativeDeadline(limits.Deadline);

        if (!_engines.TryResolve(request.Engine, out var engine))
        {
            return Fail(request, WorkerFailureKinds.EngineUnavailable, WorkerErrorCodes.EngineNotLoaded,
                "İstenen XSLT motoru bu worker'da yok.");
        }

        if (bodies.Count != (render ? 2 : 1))
            return Fail(request, WorkerFailureKinds.EngineInternal, WorkerHostErrorCodes.BadRequest, "İstek gövdeleri geçersiz.");

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bodies[0]));
        if (!string.IsNullOrEmpty(request.XsltSha256) && !string.Equals(request.XsltSha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(request, WorkerFailureKinds.EngineInternal, WorkerHostErrorCodes.Sha256Mismatch,
                "İstek bütünlüğü doğrulanamadı.");
        }

        if (!TryDecode(bodies, render, out var xslt, out var xml))
            return Fail(request, WorkerFailureKinds.EngineInternal, WorkerHostErrorCodes.BadRequest, "İstek gövdeleri geçerli UTF-8 değil.");

        if (screen && XsltSafety.Scan(xslt, XsltSafetyMode.Execute) is { HasFindings: true } report)
            return Fail(request, ResponseFailures.FromFinding(report.Findings[0]), timings: null, cacheHit: false);

        ICompiledXslt compiled;
        bool cacheHit;
        double compileMs;
        var compileWatch = Stopwatch.StartNew();
        try
        {
            (compiled, cacheHit, compileMs) = GetOrCompile(engine, sha256, xslt, limits);
        }
        catch (XsltProcessingException ex) when (!RequestHandler.IsOutOfMemory(ex))
        {
            return Fail(request, ResponseFailures.FromDiagnostic(ex.Diagnostic),
                new ResponseTimings { Compile = compileWatch.Elapsed.TotalMilliseconds }, cacheHit: false);
        }

        if (!render)
            return Ok(request, HandlerResult.NoBodies, new ResponseTimings { Compile = compileMs }, cacheHit);

        if (deadline.ForTransform(limits) is not { } transformLimits)
        {
            return Fail(request, ResponseFailures.Of(WorkerFailureKinds.Timeout, WorkerHostErrorCodes.CooperativeDeadline,
                "XSLT işlemi süre sınırını aştı."), new ResponseTimings { Compile = compileMs }, cacheHit);
        }

        return Transform(request, compiled, xml!, transformLimits, compileMs, cacheHit);
    }

    private (ICompiledXslt Compiled, bool CacheHit, double CompileMs) GetOrCompile(
        IXsltEngine engine, string sha256, string xslt, XsltExecutionLimits limits)
    {
        var key = CompileKey.For(engine.Kind, sha256, limits);
        if (_cache.TryGet(key, out var cached))
            return (cached, true, 0);

        var sw = Stopwatch.StartNew();
        var compiled = engine.Compile(xslt, limits); // hata → XsltProcessingException; önbelleğe yalnız başarı girer
        var compileMs = sw.Elapsed.TotalMilliseconds;
        _cache.Add(key, compiled);
        return (compiled, false, compileMs);
    }

    private static HandlerResult Transform(
        RequestHeader request, ICompiledXslt compiled, string xml, XsltExecutionLimits limits, double compileMs, bool cacheHit)
    {
        var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        XsltTransformTimings timings;
        try
        {
            timings = compiled.Transform(xml, output, limits);
        }
        catch (XsltProcessingException ex) when (!RequestHandler.IsOutOfMemory(ex))
        {
            return Fail(request, ResponseFailures.FromDiagnostic(ex.Diagnostic), new ResponseTimings { Compile = compileMs }, cacheHit);
        }

        var sw = Stopwatch.StartNew();
        byte[] body;
        try
        {
            body = WorkerWire.EncodeBody(output.ToString());
        }
        catch (EncoderFallbackException)
        {
            return Fail(request, ResponseFailures.Of(WorkerFailureKinds.Runtime, WorkerHostErrorCodes.InvalidOutput,
                "Çıktı geçersiz karakter içeriyor."), new ResponseTimings { Compile = compileMs }, cacheHit);
        }

        var result = new ResponseTimings
        {
            Parse = timings.ParseMs,
            Compile = compileMs,
            Transform = timings.TransformMs,
            Serialize = timings.SerializeMs + sw.Elapsed.TotalMilliseconds,
        };
        return Ok(request, [body], result, cacheHit);
    }

    private static bool TryDecode(IReadOnlyList<byte[]> bodies, bool render, out string xslt, out string? xml)
    {
        try
        {
            xslt = WorkerWire.DecodeBody(bodies[0]);
            xml = render ? WorkerWire.DecodeBody(bodies[1]) : null;
            return true;
        }
        catch (FrameProtocolException)
        {
            xslt = "";
            xml = null;
            return false;
        }
    }

    private static HandlerResult Ok(RequestHeader request, IReadOnlyList<ReadOnlyMemory<byte>> bodies, ResponseTimings timings, bool cacheHit)
    {
        var ok = RequestHandler.Ok(request.Id, bodies);
        return ok with { Header = ok.Header with { Engine = request.Engine, Timings = timings, CacheHit = cacheHit } };
    }

    private static HandlerResult Fail(RequestHeader request, string kind, string errorCode, string message) =>
        Fail(request, ResponseFailures.Of(kind, errorCode, message), timings: null, cacheHit: false);

    private static HandlerResult Fail(RequestHeader request, ResponseFailure failure, ResponseTimings? timings, bool cacheHit)
    {
        var result = RequestHandler.Failure(request.Id, failure);
        return result with { Header = result.Header with { Engine = request.Engine, Timings = timings, CacheHit = cacheHit } };
    }
}
