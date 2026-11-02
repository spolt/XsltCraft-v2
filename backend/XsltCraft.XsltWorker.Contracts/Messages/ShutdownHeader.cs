namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Api → worker: işini bitirip temiz çık.</summary>
public sealed record ShutdownHeader
{
    public string? Reason { get; init; }
}
