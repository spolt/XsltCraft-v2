namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Üretim op'ları. Test op'ları worker'da yalnız <c>--test-ops</c> ile açılır ve burada YER ALMAZ.</summary>
public static class WorkerOps
{
    public const string Ping = "ping";
    public const string Render = "render";
    public const string Validate = "validate";
}
