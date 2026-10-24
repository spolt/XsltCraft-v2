namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary><c>xslt.worker.exited{reason}</c> ve log için sabit, düşük kardinaliteli neden adları.</summary>
internal static class WorkerExitReasons
{
    public const string Recycled = "recycled";
    public const string Crashed = "crashed";
    public const string Timeout = "timeout";
    public const string MemoryLimit = "memory_limit";
    public const string ProtocolViolation = "protocol_violation";
    public const string ProtocolMismatch = "protocol_mismatch";
    public const string HandshakeFailed = "handshake_failed";
    public const string StartFailed = "start_failed";
    public const string Shutdown = "shutdown";
}
