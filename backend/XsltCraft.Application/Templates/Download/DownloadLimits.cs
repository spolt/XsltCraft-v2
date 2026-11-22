namespace XsltCraft.Application.Templates.Download;

/// <summary>Şablon ZIP indirme tavanları.</summary>
public static class DownloadLimits
{
    /// <summary>Seçili indirmede tek istekteki en fazla şablon sayısı.</summary>
    public const int MaxDownloadItems = 500;

    /// <summary>Bir ZIP'e girecek toplam XSLT karakter sayısı tavanı (aşılırsa 413).</summary>
    public const long MaxDownloadChars = 150_000_000;

    /// <summary>Üretilen ZIP dosyasının bayt tavanı (sıkışmayan içerikte disk kullanımını sınırlar; aşılırsa 413).</summary>
    public const long MaxArchiveBytes = 250L * 1024 * 1024;
}
