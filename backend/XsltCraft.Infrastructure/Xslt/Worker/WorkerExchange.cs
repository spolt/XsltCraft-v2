using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>Tek bir istek/yanıt alışverişinin süreç düzeyindeki sonucu.</summary>
internal enum WorkerExchangeStatus
{
    /// <summary>Geçerli yanıt alındı (içerik hatası olabilir; worker sağlıklı).</summary>
    Completed,

    /// <summary>İstek çerçeve sınırını aştı; akışa bayt yazılmadı, worker sağlıklı.</summary>
    RequestTooLarge,

    /// <summary>Sert zaman aşımı → süreç ağacı öldürüldü.</summary>
    Timeout,

    /// <summary>WorkingSet sınırı → süreç öldürüldü.</summary>
    MemoryLimit,

    /// <summary>Süreç beklenmedik şekilde çıktı (StackOverflow, yerel çökme, dış kill).</summary>
    Crashed,

    /// <summary>Bozuk/beklenmeyen çerçeve → süreç öldürüldü.</summary>
    ProtocolViolation,

    /// <summary>Havuz kapanırken öldürüldü.</summary>
    Shutdown,
}

internal sealed record WorkerExchange(WorkerExchangeStatus Status, WorkerResponse? Response, int? ExitCode, string? Detail)
{
    public bool WorkerSurvived => Status is WorkerExchangeStatus.Completed or WorkerExchangeStatus.RequestTooLarge;
}
