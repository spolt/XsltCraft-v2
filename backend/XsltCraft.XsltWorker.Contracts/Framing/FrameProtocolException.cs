namespace XsltCraft.XsltWorker.Contracts.Framing;

/// <summary>Çerçeve okunamadı/yazılamadı: akış artık güvenilmez, karşı taraf sonlandırılmalıdır.</summary>
public sealed class FrameProtocolException : Exception
{
    public FrameProtocolException(FrameError error, string message)
        : base(message)
    {
        Error = error;
    }

    public FrameError Error { get; }
}
