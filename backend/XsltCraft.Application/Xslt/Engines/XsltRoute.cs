namespace XsltCraft.Application.Xslt.Engines;

/// <summary>XSLT çalıştırma isteğinin geldiği hat; motor seçimi ve sınırlar hatta göre değişir.</summary>
public enum XsltRoute
{
    /// <summary>
    /// Kullanıcının ham XSLT'si (geliştirici modu / XSLT editörü). Motor <c>Xslt:Engine:Raw</c>
    /// (<see cref="XsltEngineSelector.SelectByUsage"/>); her iki motor da izole worker.
    /// </summary>
    Raw,

    /// <summary>Grid blok ağacından üretilen XSLT.</summary>
    Grid,

    /// <summary>
    /// Hazır tema XSLT'si (yükleme/analiz, compose, galeri önizlemesi). Motor <c>Xslt:Engine:Theme</c>
    /// (<see cref="XsltEngineSelector.SelectByUsage"/>); .NET süreç içi, Saxon izole worker.
    /// </summary>
    Theme,

    /// <summary>
    /// Kullanıcının kayıtlı şablonu (<c>POST /api/preview/user-template/{id}</c>). Motor modu grid ile
    /// aynıdır (<c>Xslt:Engine:Grid</c>) ama yürütme her zaman izole worker'dadır (ADR 007 §2).
    /// </summary>
    UserTemplate,
}
