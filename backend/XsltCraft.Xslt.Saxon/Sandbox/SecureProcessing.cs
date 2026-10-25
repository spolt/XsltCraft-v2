using net.sf.saxon;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// JAXP <c>XMLConstants.FEATURE_SECURE_PROCESSING</c>'i Saxon yapılandırmasına uygular. s9api'de doğrudan
/// karşılığı yok; Saxon'un JAXP fabrikası (<c>TransformerFactoryImpl</c>) aynı <see cref="Configuration"/>
/// üzerinde ayarı yapar. Saxon-HE 12.10'da tek etkisi <c>ALLOW_EXTERNAL_FUNCTIONS=false</c>'tur — protokol,
/// ayrıştırıcı veya resolver kısıtı DEĞİLDİR; bir sandbox katmanı sayılmaz (ADR 008). Asıl kısıtlar
/// <see cref="SaxonProcessorFactory"/>'deki açık Feature'lardır ve bu çağrıdan SONRA uygulanır.
/// </summary>
internal static class SecureProcessing
{
    public const string FeatureUri = "http://javax.xml.XMLConstants/feature/secure-processing";

    public static void Apply(Configuration config)
    {
        var factory = new TransformerFactoryImpl(config);
        factory.setFeature(FeatureUri, true);
    }
}
