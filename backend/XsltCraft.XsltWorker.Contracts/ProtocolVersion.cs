namespace XsltCraft.XsltWorker.Contracts;

/// <summary>
/// Worker IPC protokol sürümü. Hem çerçeve başlığındaki <c>ver</c> baytı hem de
/// <see cref="Messages.HelloHeader.Protocol"/> bu değeri taşır; uyuşmazlıkta worker reddedilir.
/// </summary>
public static class ProtocolVersion
{
    public const byte Current = 1;
}
