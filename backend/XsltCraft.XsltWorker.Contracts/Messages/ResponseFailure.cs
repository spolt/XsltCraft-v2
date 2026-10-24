namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Başarısız işlemin ayrıntısı. <see cref="Kind"/> <see cref="WorkerFailureKinds"/> değerlerinden biridir.</summary>
public sealed record ResponseFailure
{
    public string Kind { get; init; } = WorkerFailureKinds.EngineInternal;

    public string Message { get; init; } = "";

    public int? Line { get; init; }

    public int? Column { get; init; }

    /// <summary>Makine-okur ayrıntı kodu (ör. <c>worker.timeout</c>, Saxon <c>XTDE0640</c>).</summary>
    public string? ErrorCode { get; init; }
}
