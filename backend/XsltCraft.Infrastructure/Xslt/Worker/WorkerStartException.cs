namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>Worker başlatılamadı ya da el sıkışma başarısız (süreç öldürülmüş olarak fırlatılır).</summary>
internal sealed class WorkerStartException : Exception
{
    public WorkerStartException(string reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary><see cref="WorkerExitReasons"/> değerlerinden biri.</summary>
    public string Reason { get; }
}
