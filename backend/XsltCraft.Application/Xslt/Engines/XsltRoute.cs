namespace XsltCraft.Application.Xslt.Engines;

/// <summary>XSLT çalıştırma isteğinin geldiği hat; motor seçimi ve sınırlar hatta göre değişir.</summary>
public enum XsltRoute
{
    /// <summary>Kullanıcının ham XSLT'si (geliştirici modu / XSLT editörü).</summary>
    Raw,

    /// <summary>Grid blok ağacından üretilen XSLT.</summary>
    Grid,

    /// <summary>Hazır tema XSLT'si.</summary>
    Theme,
}
