namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Politika + bildirilen sürüm → motor. <see cref="XsltEngineMode.Auto"/>: sürüm &gt;= 2.0 → Saxon;
/// &lt; 2.0 veya okunamaz (<c>null</c>) → .NET. Desteklenmeyen sürümleri (&gt; 2.0) reddetmek
/// güvenlik taramasının işidir, seçicinin değil.
/// </summary>
public static class XsltEngineSelector
{
    private const decimal SaxonMinimumVersion = 2.0m;

    public static XsltEngineKind Select(XsltEngineMode mode, decimal? sniffedVersion) => mode switch
    {
        XsltEngineMode.DotNet => XsltEngineKind.DotNet,
        XsltEngineMode.Saxon => XsltEngineKind.Saxon,
        XsltEngineMode.Auto => sniffedVersion >= SaxonMinimumVersion ? XsltEngineKind.Saxon : XsltEngineKind.DotNet,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Bilinmeyen XSLT motor modu."),
    };
}
