namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Worker içi işbirlikçi sınırlar (Api tarafındaki zaman aşımı/bellek bekçisi bunlardan bağımsız sert sınırdır).</summary>
public sealed record RequestLimits
{
    public int DeadlineMs { get; init; }

    public long MaxOutputChars { get; init; }

    public int MaxStylesheetDepth { get; init; }

    public int MaxInputDepth { get; init; }

    public long MaxInputNodes { get; init; }
}
