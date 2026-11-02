using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Havuza verilen istek. Worker rota bilmez: yalnız op + motor + gövdeler.
/// </summary>
/// <param name="Op"><see cref="WorkerOps"/> değeri.</param>
/// <param name="Engine"><see cref="WorkerEngines"/> değeri.</param>
/// <param name="XsltSha256">XSLT özeti (hex) — yumuşak afinite + worker önbelleği anahtarı; yalnız ilk 16 hanesi loglanır.</param>
/// <param name="Bodies">Ham metin gövdeleri (render → [xslt, xml]); UTF-8 olarak gönderilir.</param>
public sealed record XsltWorkerRequest(string Op, string? Engine, string? XsltSha256, IReadOnlyList<string> Bodies)
{
    public RequestLimits? Limits { get; init; }

    /// <summary>Op varsayılanı yerine kullanılacak sert zaman aşımı (ör. grid 8 sn).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Yalnız test worker'larında (<c>--test-ops</c>) anlamlı parametre.</summary>
    internal long? TestArgument { get; init; }
}

/// <summary>Havuz sonucu: worker yanıtı (veya havuzun sentezlediği hata) + hizmet eden worker.</summary>
public sealed record XsltWorkerPoolResult(WorkerResponse Response, int? WorkerPid)
{
    public bool Ok => Response.Header.Ok;

    public string? FailureKind => Response.Header.Failure?.Kind;

    public string? ErrorCode => Response.Header.Failure?.ErrorCode;
}
