using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>Bir isteğin işlenmiş sonucu: yanıt başlığı + ham gövdeler.</summary>
internal sealed record HandlerResult(ResponseHeader Header, IReadOnlyList<ReadOnlyMemory<byte>> Bodies)
{
    public static readonly IReadOnlyList<ReadOnlyMemory<byte>> NoBodies = [];
}
