namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Çözülmüş yanıt: başlık + (varsa) HTML çıktısı.</summary>
public sealed record WorkerResponse(ResponseHeader Header, string? Output)
{
    public static WorkerResponse Failed(string id, string kind, string errorCode, string message) =>
        new(new ResponseHeader
        {
            Id = id,
            Ok = false,
            Failure = new ResponseFailure { Kind = kind, ErrorCode = errorCode, Message = message },
        }, null);
}
