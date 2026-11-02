using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Xslt;

/// <summary>
/// XslCompiledTransform için tek güvenli derleme noktası. Stylesheet'ler (kullanıcı/admin
/// içeriği dahil) HER ZAMAN buradan derlenmelidir.
///
/// - DTD yasak, reader'da XmlResolver yok (XXE).
/// - <c>document()</c> ve script kapalı.
/// - Stylesheet resolver <c>null</c>: <c>xsl:import</c>/<c>xsl:include</c> hiç çözümlenmez.
///   <c>XmlUrlResolver</c> verilirse <c>href="file:///..."</c> yerel dosya okur, <c>http://</c>
///   iç ağa istek atar (SSRF), <c>\\sunucu\pay</c> NTLM kimliği sızdırır; base URI olmadığı
///   için göreli href bile sunucunun çalışma dizinine göre çözülür.
/// </summary>
public static class SecureXslt
{
    public static XsltSettings Settings => new(enableDocumentFunction: false, enableScript: false);

    public static XmlReaderSettings ReaderSettings() =>
        new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    public static XslCompiledTransform Compile(string xslt)
    {
        var transform = new XslCompiledTransform();
        using var reader = XmlReader.Create(new StringReader(xslt), ReaderSettings());
        transform.Load(reader, Settings, stylesheetResolver: null);
        return transform;
    }

    /// <summary>
    /// <see cref="Compile"/> ile aynı güvenli derleme; stylesheet aynı okuma geçişinde
    /// <see cref="Engines.LimitedXmlReader"/> ile en fazla <paramref name="maxElementDepth"/> eleman
    /// seviyesine sınırlanır (ihlal → <see cref="Engines.XsltProcessingException"/>, LimitExceeded).
    /// </summary>
    public static XslCompiledTransform CompileWithDepthLimit(string xslt, int maxElementDepth)
    {
        var transform = new XslCompiledTransform();
        using var reader = new Engines.LimitedXmlReader(
            XmlReader.Create(new StringReader(xslt), ReaderSettings()),
            maxElementDepth, long.MaxValue, Engines.XsltFailureKind.LimitExceeded, Engines.XsltEngineKind.DotNet, "XSLT");
        transform.Load(reader, Settings, stylesheetResolver: null);
        return transform;
    }
}
