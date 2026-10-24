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

    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>
    /// Ön okuma; ihlalde <see cref="XsltProcessingException"/>. Aynı geçişte statik ifadeler (use-when, gölge
    /// <c>_*</c> öznitelikleri, <c>static</c>≠no) motor düzeyinde reddedilir (Security,
    /// <see cref="SandboxCodes.StylesheetStaticExpression"/>): Saxon bunları XPath 3.1 statik bağlamında derler
    /// (XPATH_VERSION_FOR_XSLT=20 tavanına tabi DEĞİL) → derleme anı özyinelemesi süreci düşürebilir (G2 security #1).
    /// İki profil (Isolated, InProcessGrid) bu yoldan geçer; XsltSafety taramasından bağımsızdır.
    /// </summary>
    /// <param name="screenStaticExpressions">Yalnız test seam'i (LayeredSandbox Saxon katmanlarını tek tek ölçer).</param>
    public static void PreRead(string xslt, int maxDepth, long maxCharacters, bool screenStaticExpressions = true)
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
                if (screenStaticExpressions && reader.NodeType == XmlNodeType.Element)
                    ScreenStaticExpressions(reader);
            }
        }
        catch (XsltProcessingException ex) when (ex.Diagnostic.ErrorCode is null && ex.Diagnostic.Kind == XsltFailureKind.LimitExceeded)
        {
            // LimitedXmlReader kod taşımaz; ön okumada düğüm sınırı yok → yalnız derinlik ihlali.
            throw new XsltProcessingException(ex.Diagnostic with { ErrorCode = Limits.LimitCodes.StylesheetDepth }, ex);
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

    // XSL elemanında: ad alanısız use-when / _* / static≠no. Her elemanda (LRE dahil): xsl:use-when / xsl:_*.
    private static void ScreenStaticExpressions(XmlReader reader)
    {
        var xslElement = reader.NamespaceURI == XslNs;
        if (!reader.MoveToFirstAttribute())
            return;
        try
        {
            do
            {
                var ns = reader.NamespaceURI;
                var local = reader.LocalName;
                var rejected = ns.Length == 0
                    ? xslElement && (IsStaticName(local) || (local == "static" && !IsFalse(reader.Value)))
                    : ns == XslNs && IsStaticName(local);
                if (rejected)
                    throw StaticRejection(reader, reader.Name);
            }
            while (reader.MoveToNextAttribute());
        }
        finally
        {
            reader.MoveToElement();
        }
    }

    private static bool IsStaticName(string local) => local == "use-when" || local.StartsWith('_');

    private static bool IsFalse(string value) => value.Trim().ToLowerInvariant() is "no" or "false" or "0";

    private static XsltProcessingException StaticRejection(XmlReader reader, string attribute)
    {
        var li = reader as IXmlLineInfo;
        var has = li?.HasLineInfo() == true;
        return new XsltProcessingException(new XsltDiagnostic(
            $"Stylesheet statik ifade içeremez ({attribute}).", has ? li!.LineNumber : 0, has ? li!.LinePosition : 0,
            XsltFailureKind.Security, XsltEngineKind.Saxon, SandboxCodes.StylesheetStaticExpression));
    }

    // .NET: "For security reasons DTD is prohibited in this XML document..." (Xml_DtdIsProhibitedEx).
    // .NET çalışma zamanı XML kaynak dizgilerini yerelleştirmez; tanınmasa bile hata Compile olarak reddedilir (fail-closed).
    private static bool IsDtdRejection(XmlException ex) =>
        ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase);

    private static bool IsCharacterLimit(XmlException ex) =>
        ex.Message.Contains("MaxCharactersInDocument", StringComparison.Ordinal);
}
