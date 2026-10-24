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

    /// <summary>Bellek tükendi: <c>MemoryLimit</c> + <c>recycleRequested</c> yanıtı yazıldıktan sonra çıkış.</summary>
    public const int MemoryExhausted = 4;

    /// <summary>Motor kurulumu/ısınması başarısız: Hello GÖNDERİLMEDEN çıkış (havuz başlatma hatası sayar).</summary>
    public const int WarmupFailed = 5;

    /// <summary>Tanınmayan komut satırı argümanı.</summary>
    public const int BadArguments = 64;
}
