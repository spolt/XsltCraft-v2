using System.Xml;
using javax.xml.transform;
using javax.xml.transform.sax;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon.Input;

/// <summary>
/// Stylesheet'i Saxon derleyicisine güvenli biçimde verir. Saxon'un parse seçeneklerindeki
/// <c>disallow-doctype-decl</c> stylesheet'e UYGULANMAZ (G0 Y-1: iç entity 30 KB → 30M karakter). Bu yüzden:
/// <list type="number">
/// <item>Birincil katman: derlemeden ÖNCE .NET <see cref="XmlReader"/> (<see cref="SecureXslt.ReaderSettings"/>)
/// ile tam okuma — DOCTYPE → Security, derinlik/karakter sınırı → LimitExceeded, bozuk XML → Compile.</item>
/// <item>İkinci katman: Saxon'a JDK SAX ayrıştırıcısı <c>disallow-doctype-decl=true</c> ile verilir
/// (satır/kolon bilgisi korunur).</item>
/// </list>
/// </summary>
internal static class StylesheetSource
{
    public const string SystemId = "urn:xsltcraft:stylesheet";
    private const string DisallowDoctype = "http://apache.org/xml/features/disallow-doctype-decl";

    /// <summary>Ön okuma; ihlalde <see cref="XsltProcessingException"/>.</summary>
    public static void PreRead(string xslt, int maxDepth, long maxCharacters)
    {
        var settings = SecureXslt.ReaderSettings();
        settings.MaxCharactersInDocument = maxCharacters;
        try
        {
            using var reader = new LimitedXmlReader(
                XmlReader.Create(new StringReader(xslt), settings),
                maxDepth, long.MaxValue, XsltFailureKind.LimitExceeded, XsltEngineKind.Saxon, "XSLT");
            while (reader.Read())
            {
            }
        }
        catch (XmlException ex) when (IsDtdRejection(ex))
        {
            throw new XsltProcessingException(new XsltDiagnostic(
                "Stylesheet DOCTYPE/DTD içeremez.", ex.LineNumber, ex.LinePosition,
                XsltFailureKind.Security, XsltEngineKind.Saxon, SandboxCodes.StylesheetDtd), ex);
        }
        catch (XmlException ex) when (IsCharacterLimit(ex))
        {
            throw new XsltProcessingException(new XsltDiagnostic(
                $"Stylesheet boyut sınırı aşıldı (en fazla {maxCharacters} karakter).", ex.LineNumber, ex.LinePosition,
                XsltFailureKind.LimitExceeded, XsltEngineKind.Saxon, Limits.LimitCodes.StylesheetChars), ex);
        }
        catch (XmlException ex)
        {
            throw new XsltProcessingException(
                XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Compile, XsltEngineKind.Saxon), ex);
        }
    }

    /// <summary>Saxon'a verilecek kaynak: DOCTYPE'ı reddeden JDK SAX okuyucusu + sabit systemId.</summary>
    public static Source Create(string xslt)
    {
        var factory = javax.xml.parsers.SAXParserFactory.newInstance();
        factory.setNamespaceAware(true);
        factory.setValidating(false);
        factory.setXIncludeAware(false);
        factory.setFeature(DisallowDoctype, true);
        factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
        factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);

        var input = new org.xml.sax.InputSource(new java.io.StringReader(xslt));
        input.setSystemId(SystemId);
        var source = new SAXSource(factory.newSAXParser().getXMLReader(), input);
        source.setSystemId(SystemId);
        return source;
    }

    // .NET: "For security reasons DTD is prohibited in this XML document..." (Xml_DtdIsProhibitedEx).
    // .NET çalışma zamanı XML kaynak dizgilerini yerelleştirmez; tanınmasa bile hata Compile olarak reddedilir (fail-closed).
    private static bool IsDtdRejection(XmlException ex) =>
        ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase);

    private static bool IsCharacterLimit(XmlException ex) =>
        ex.Message.Contains("MaxCharactersInDocument", StringComparison.Ordinal);
}
