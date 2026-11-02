namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>
/// İstek başlığı. Gövdeler (ham UTF-8): <c>render</c> → [xslt, xml]; <c>validate</c> → [xslt]; <c>ping</c> → [].
/// Worker rota bilmez; yalnız <see cref="Op"/> + <see cref="Engine"/> alır.
/// </summary>
public sealed record RequestHeader
{
    public string Id { get; init; } = "";

    /// <summary><see cref="WorkerOps"/> değerlerinden biri.</summary>
    public string Op { get; init; } = "";

    /// <summary><see cref="WorkerEngines"/> değerlerinden biri (<c>ping</c> için yok sayılır).</summary>
    public string? Engine { get; init; }

    /// <summary>XSLT gövdesinin SHA-256'sı (hex) — derleme önbelleği / afinite anahtarı.</summary>
    public string? XsltSha256 { get; init; }

    public RequestLimits? Limits { get; init; }

    /// <summary>Yalnız worker <c>--test-ops</c> ile açıldığında anlamlı sayısal test parametresi (ms, MB…).</summary>
    public long? TestArgument { get; init; }
}
