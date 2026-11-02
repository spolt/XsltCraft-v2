namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Yanıt başlığı. Başarılı <c>render</c> → gövde [html]; diğerleri → [].</summary>
public sealed record ResponseHeader
{
    public string Id { get; init; } = "";

    public bool Ok { get; init; }

    public ResponseFailure? Failure { get; init; }

    public ResponseTimings? Timings { get; init; }

    public string? Engine { get; init; }

    public bool CacheHit { get; init; }

    public long WorkingSetBytes { get; init; }

    public long GcHeapBytes { get; init; }

    /// <summary>Worker kendini sağlıksız sayıyor (ör. OOM sonrası); havuz geri dönüştürür.</summary>
    public bool RecycleRequested { get; init; }
}
