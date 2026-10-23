using System.Xml;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// XML'i DOM kurmadan, XXE korumalı okuyucuyla tarayıp eleman derinliği ve düğüm sayısını
/// sınırlar. Ayrıştırma hatası burada raporlanmaz: asıl yükleyici (XmlDocument / XslCompiledTransform)
/// aynı hatayı kendi mesajıyla verir, böylece hata metinleri mevcut davranışla aynı kalır.
/// </summary>
internal static class XmlShapeGuard
{
    public static void Ensure(string xml, int maxDepth, int maxNodes, XsltFailureKind kind, XsltEngineKind engine, string what)
    {
        using var reader = XmlReader.Create(new StringReader(xml), SecureXslt.ReaderSettings());
        var lineInfo = reader as IXmlLineInfo;
        long nodes = 0;

        try
        {
            while (reader.Read())
            {
                nodes += 1 + reader.AttributeCount;
                if (reader.NodeType == XmlNodeType.Element && reader.Depth + 1 > maxDepth)
                    throw Fail($"{what} derinlik sınırı aşıldı (en fazla {maxDepth}).", lineInfo, kind, engine);
                if (nodes > maxNodes)
                    throw Fail($"{what} düğüm sayısı sınırı aşıldı (en fazla {maxNodes}).", lineInfo, kind, engine);
            }
        }
        catch (XmlException)
        {
            // Bilinçli: bkz. sınıf açıklaması.
        }
    }

    private static XsltProcessingException Fail(string message, IXmlLineInfo? li, XsltFailureKind kind, XsltEngineKind engine) =>
        new(new XsltDiagnostic(message, li?.LineNumber ?? 0, li?.LinePosition ?? 0, kind, engine));
}
