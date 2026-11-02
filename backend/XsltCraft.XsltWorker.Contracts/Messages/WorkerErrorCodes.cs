namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Worker/havuz kaynaklı <see cref="ResponseFailure.ErrorCode"/> değerleri.</summary>
public static class WorkerErrorCodes
{
    public const string UnknownOp = "worker.unknown_op";
    public const string EngineNotLoaded = "worker.engine_not_loaded";
    public const string OutOfMemory = "worker.out_of_memory";
    public const string Timeout = "worker.timeout";
    public const string MemoryLimit = "worker.memory_limit";
    public const string Crashed = "worker.crashed";
    public const string ProtocolViolation = "worker.protocol_violation";
    public const string QueueFull = "worker.queue_full";
    public const string QueueTimeout = "worker.queue_timeout";
    public const string NoReadyWorker = "worker.no_ready_worker";
    public const string BreakerOpen = "worker.breaker_open";
    public const string Disabled = "worker.disabled";
    public const string RequestTooLarge = "worker.request_too_large";
}
