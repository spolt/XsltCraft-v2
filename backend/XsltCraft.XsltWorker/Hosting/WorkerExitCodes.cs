namespace XsltCraft.XsltWorker.Hosting;

/// <summary>Worker çıkış kodları (Api yalnız loglar; içerik taşımaz).</summary>
internal static class WorkerExitCodes
{
    /// <summary>stdin EOF veya Shutdown çerçevesi.</summary>
    public const int Normal = 0;

    /// <summary>Api'den bozuk/beklenmeyen çerçeve geldi.</summary>
    public const int ProtocolError = 2;

    /// <summary>stdout'a yazılamadı (Api tarafı kapandı).</summary>
    public const int OutputClosed = 3;

    /// <summary>Tanınmayan komut satırı argümanı.</summary>
    public const int BadArguments = 64;
}
