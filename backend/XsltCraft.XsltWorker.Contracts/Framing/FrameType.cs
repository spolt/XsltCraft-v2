namespace XsltCraft.XsltWorker.Contracts.Framing;

/// <summary>Çerçeve türü (tel üzerinde 1 bayt).</summary>
public enum FrameType : byte
{
    Hello = 1,
    Request = 2,
    Response = 3,
    Shutdown = 4,
}
