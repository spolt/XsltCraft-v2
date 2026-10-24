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

    /// <summary>
    /// Kullanıcının kayıtlı şablonu (<c>POST /api/preview/user-template/{id}</c>). Motor modu grid ile
    /// aynıdır (<c>Xslt:Engine:Grid</c>) ama yürütme her zaman izole worker'dadır (ADR 007 §2).
    /// </summary>
    UserTemplate,
}
