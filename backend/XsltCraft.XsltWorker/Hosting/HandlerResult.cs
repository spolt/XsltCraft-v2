using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Bir isteğin işlenmiş sonucu: yanıt başlığı + ham gövdeler. <see cref="ExitAfterResponse"/> → yanıt yazıldıktan
/// sonra süreç çıkar (bellek tükenmesi: durum güvenilmez, havuz zaten <c>recycleRequested</c> ile yenisini başlatır).
/// </summary>
internal sealed record HandlerResult(ResponseHeader Header, IReadOnlyList<ReadOnlyMemory<byte>> Bodies)
{
    public static readonly IReadOnlyList<ReadOnlyMemory<byte>> NoBodies = [];

    public bool ExitAfterResponse { get; init; }
}
