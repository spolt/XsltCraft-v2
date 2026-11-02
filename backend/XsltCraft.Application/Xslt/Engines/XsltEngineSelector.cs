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

    /// <summary>
    /// Ham XSLT ve tema hattı: <see cref="XsltEngineMode.Auto"/> yalnız <c>version ≥ 2.0</c> VE .NET'in
    /// çalıştıramayacağı 2.0 yapısı (<see cref="Xslt2FeatureDetector"/>) varsa Saxon'dur — <c>version="2.0"</c> bildirip
    /// 1.0 semantiğine güvenen stylesheet'ler (GİB şablonları: çok düğümlü <c>format-number</c>, <c>value-of</c>) .NET'te
    /// kalır, çıktı bit-aynıdır. Diğer modlar <see cref="Select"/> ile aynıdır.
    /// </summary>
    public static XsltEngineKind SelectByUsage(XsltEngineMode mode, decimal? sniffedVersion, string xslt) =>
        mode == XsltEngineMode.Auto
            ? sniffedVersion >= SaxonMinimumVersion && Xslt2FeatureDetector.UsesXslt2Features(xslt) ? XsltEngineKind.Saxon : XsltEngineKind.DotNet
            : Select(mode, sniffedVersion);
}
