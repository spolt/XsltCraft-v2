namespace XsltCraft.XsltWorker.Contracts;

/// <summary>Çerçeve boyut sınırları. İhlal eden çerçeve okunmaz/yazılmaz; Api tarafında worker öldürülür.</summary>
public static class FrameLimits
{
    /// <summary>Api → worker istek çerçevesi üst sınırı (12 MB).</summary>
    public const int MaxRequestFrameBytes = 12 * 1024 * 1024;

    /// <summary>Worker → Api yanıt (ve Hello) çerçevesi üst sınırı (24 MB).</summary>
    public const int MaxResponseFrameBytes = 24 * 1024 * 1024;

    /// <summary>JSON başlık üst sınırı (64 KB).</summary>
    public const int MaxHeaderBytes = 64 * 1024;

    /// <summary>Bir çerçevedeki ham gövde sayısı üst sınırı.</summary>
    public const int MaxBodyCount = 8;
}
