namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Worker içi aşama süreleri (ms).</summary>
public sealed record ResponseTimings
{
    public double Parse { get; init; }

    public double Compile { get; init; }

    public double Transform { get; init; }

    public double Serialize { get; init; }
}
