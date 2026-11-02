namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>
/// Telde taşınan hata sınıfı adları. Application'daki <c>XsltFailureKind</c> enum üyeleriyle birebir
/// aynı yazılır (Contracts bağımlılıksız kaldığı için enum paylaşılmaz; W2 adaptörü adla eşler).
/// </summary>
public static class WorkerFailureKinds
{
    public const string Xml = "Xml";
    public const string Compile = "Compile";
    public const string Runtime = "Runtime";
    public const string Security = "Security";
    public const string Unsupported = "Unsupported";
    public const string Timeout = "Timeout";
    public const string MemoryLimit = "MemoryLimit";
    public const string OutputLimit = "OutputLimit";
    public const string LimitExceeded = "LimitExceeded";
    public const string EngineCrashed = "EngineCrashed";
    public const string Busy = "Busy";
    public const string UserBusy = "UserBusy";
    public const string EngineUnavailable = "EngineUnavailable";
    public const string EngineInternal = "EngineInternal";
}
