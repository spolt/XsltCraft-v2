using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// <see cref="IXsltIsolatedHost"/> → <see cref="XsltWorkerPool"/> adaptörü. Worker rota bilmez:
/// yalnız op + motor + gövdeler (render → [xslt, xml]; validate → [xslt]) + sınırlar.
/// <list type="bullet">
/// <item>Sert süre: <see cref="XsltExecutionLimits.Deadline"/> (havuz süreci öldürür). Worker'a giden
///   işbirlikçi deadline bundan <see cref="CooperativeMargin"/> kısadır — worker temiz <c>Timeout</c>
///   yanıtı verebilsin, kill/respawn gerekmesin.</item>
/// <item><see cref="WorkerFailureKinds"/> (string) → <see cref="XsltFailureKind"/> birebir ad eşlemesi;
///   bilinmeyen ad (sürüm kayması) → <see cref="XsltFailureKind.EngineInternal"/>.</item>
/// </list>
/// </summary>
public sealed class WorkerXsltHost : IXsltIsolatedHost
{
    internal static readonly TimeSpan CooperativeMargin = TimeSpan.FromMilliseconds(500);

    // Enum.TryParse sayısal dizgeleri ("3") ve büyük/küçük harf farkını da kabul ederdi — ad tablosu kullanılır.
    private static readonly FrozenDictionary<string, XsltFailureKind> Kinds =
        Enum.GetValues<XsltFailureKind>().ToFrozenDictionary(k => k.ToString(), StringComparer.Ordinal);

    private readonly IXsltWorkerPool _pool;
    private readonly IOptions<XsltWorkerOptions> _options;

    public WorkerXsltHost(IXsltWorkerPool pool, IOptions<XsltWorkerOptions> options)
    {
        _pool = pool;
        _options = options;
    }

    public bool IsEnabled => _options.Value.Enabled;

    public async Task<XsltHostResult> ExecuteAsync(XsltHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var render = request.Op == XsltOperation.Render;
        var limits = request.Limits;
        var workerRequest = new XsltWorkerRequest(
            render ? WorkerOps.Render : WorkerOps.Validate,
            ToWire(request.Engine),
            Sha256Hex(request.Xslt),
            render ? [request.Xslt, request.Xml ?? ""] : [request.Xslt])
        {
            Timeout = limits.Deadline,
            Limits = new RequestLimits
            {
                DeadlineMs = CooperativeDeadlineMs(limits.Deadline),
                MaxOutputChars = limits.MaxOutputChars,
                MaxStylesheetDepth = limits.MaxStylesheetDepth,
                MaxInputDepth = limits.MaxInputDepth,
                MaxInputNodes = limits.MaxInputNodes,
            },
        };

        var result = await _pool.ExecuteAsync(workerRequest, cancellationToken).ConfigureAwait(false);
        return Map(result.Response.Header, result.Response.Output, request.Engine, render);
    }

    /// <summary>Tel adı → enum; tanımsız/boş → <see cref="XsltFailureKind.EngineInternal"/>.</summary>
    internal static XsltFailureKind MapKind(string? kind) =>
        kind is not null && Kinds.TryGetValue(kind, out var mapped) ? mapped : XsltFailureKind.EngineInternal;

    internal static XsltHostResult Map(ResponseHeader header, string? output, XsltEngineKind requested, bool render)
    {
        var engine = FromWire(header.Engine) ?? requested;
        var timings = header.Timings is { } t
            ? new XsltTransformTimings(Ms(t.Parse), Ms(t.Compile), Ms(t.Transform), Ms(t.Serialize))
            : null;

        if (header.Ok)
            return new XsltHostResult(render ? output ?? "" : null, engine, timings, header.CacheHit, null);

        var failure = header.Failure;
        var diagnostic = new XsltDiagnostic(
            string.IsNullOrWhiteSpace(failure?.Message) ? "XSLT motoru isteği tamamlayamadı." : failure.Message,
            failure?.Line ?? 0,
            failure?.Column ?? 0,
            MapKind(failure?.Kind),
            engine,
            failure?.ErrorCode);
        return new XsltHostResult(null, engine, timings, header.CacheHit, diagnostic);
    }

    private static int CooperativeDeadlineMs(TimeSpan hard)
    {
        var cooperative = hard - CooperativeMargin;
        if (cooperative <= TimeSpan.Zero)
            cooperative = hard;
        return (int)Math.Clamp(cooperative.TotalMilliseconds, 1, int.MaxValue);
    }

    private static string ToWire(XsltEngineKind engine) => engine switch
    {
        XsltEngineKind.DotNet => WorkerEngines.DotNet,
        XsltEngineKind.Saxon => WorkerEngines.Saxon,
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null),
    };

    private static XsltEngineKind? FromWire(string? engine) => engine switch
    {
        WorkerEngines.DotNet => XsltEngineKind.DotNet,
        WorkerEngines.Saxon => XsltEngineKind.Saxon,
        _ => null,
    };

    private static long Ms(double value) => double.IsFinite(value) && value > 0 ? (long)Math.Round(value) : 0;

    private static string Sha256Hex(string xslt) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(xslt)));
}
