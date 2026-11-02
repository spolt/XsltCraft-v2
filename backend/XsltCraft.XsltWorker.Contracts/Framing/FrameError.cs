namespace XsltCraft.XsltWorker.Contracts.Framing;

/// <summary>Protokol ihlali nedeni.</summary>
public enum FrameError
{
    BadMagic,
    UnsupportedVersion,
    UnknownType,
    HeaderTooLarge,
    FrameTooLarge,
    TooManyBodies,
    Truncated,
    InvalidHeader,
    InvalidBody,
}
