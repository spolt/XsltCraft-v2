using System.Xml;
using net.sf.saxon.s9api;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Input;

/// <summary>Girdi ağacı sınırları.</summary>
internal sealed record TreeBridgeLimits(int MaxDepth, long MaxNodes, long MaxCharacters);

/// <summary>
/// Girdi XML'ini Saxon'un ayrıştırıcısına HİÇ vermeden, .NET <see cref="XmlReader"/> ile (DTD yasak, resolver yok —
/// <see cref="SecureXslt.ReaderSettings"/>) okuyup <c>DocumentBuilder.newBuildingStreamWriter()</c> (StAX) üzerinden
/// Saxon ağacına aktarır. Derinlik/düğüm sınırı <see cref="LimitedXmlReader"/> ile, karakter sınırı
/// <c>MaxCharactersInDocument</c> ile aynı okuma geçişinde uygulanır. Hata durumunda okuyucu ve yazıcı
/// her koşulda kapatılır.
/// </summary>
internal sealed class XmlReaderTreeBridge(Processor processor, WhitespaceStrippingPolicy? whitespacePolicy = null)
{
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
    private const string DefaultBaseUri = "urn:xsltcraft:input";

    public XdmNode Build(string xml, TreeBridgeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return BuildOwned(XmlReader.Create(new StringReader(xml), CreateSettings(limits)), limits);
    }

    /// <summary>Akışın sahipliği köprüye geçer: başarıda da hatada da kapatılır.</summary>
    public XdmNode Build(Stream stream, TreeBridgeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var settings = CreateSettings(limits);
        settings.CloseInput = true;
        return BuildOwned(XmlReader.Create(stream, settings), limits);
    }

    /// <summary>
    /// Çağıranın okuyucusu. DTD işleme <see cref="DtdProcessing.Prohibit"/> değilse reddedilir (fail-closed);
    /// okuyucu dispose edilmez, ama derinlik/düğüm sınırı uygulanır.
    /// </summary>
    public XdmNode Build(XmlReader reader, TreeBridgeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Settings is not { DtdProcessing: DtdProcessing.Prohibit })
            throw new ArgumentException("Okuyucu DtdProcessing.Prohibit ile oluşturulmalı (SecureXslt.ReaderSettings).", nameof(reader));

        var limited = Limit(reader, limits);
        return Copy(limited);
    }

    private XdmNode BuildOwned(XmlReader reader, TreeBridgeLimits limits)
    {
        using var limited = Limit(reader, limits);
        return Copy(limited);
    }

    private static LimitedXmlReader Limit(XmlReader reader, TreeBridgeLimits limits) =>
        new(reader, limits.MaxDepth, limits.MaxNodes, XsltFailureKind.Xml, XsltEngineKind.Saxon, "XML");

    private static XmlReaderSettings CreateSettings(TreeBridgeLimits limits)
    {
        var settings = SecureXslt.ReaderSettings();
        settings.MaxCharactersInDocument = limits.MaxCharacters;
        return settings;
    }

    private XdmNode Copy(XmlReader reader)
    {
        var builder = processor.newDocumentBuilder();
        builder.setBaseURI(new java.net.URI(DefaultBaseUri));
        if (whitespacePolicy is not null)
            builder.setWhitespaceStrippingPolicy(whitespacePolicy);

        var writer = builder.newBuildingStreamWriter();
        var completed = false;
        try
        {
            writer.writeStartDocument();
            while (reader.Read())
                CopyNode(reader, writer);
            writer.writeEndDocument();
            completed = true;
            return writer.getDocumentNode();
        }
        finally
        {
            try
            {
                writer.close();
            }
            catch (Exception) when (!completed)
            {
                // Asıl hata (sınır / XML) korunur; yarım ağacın kapanış hatası yutulur.
            }
        }
    }

    private static void CopyNode(XmlReader reader, BuildingStreamWriter writer)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                var empty = reader.IsEmptyElement;
                writer.writeStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                CopyAttributes(reader, writer);
                if (empty)
                    writer.writeEndElement();
                break;
            case XmlNodeType.EndElement:
                writer.writeEndElement();
                break;
            case XmlNodeType.Text:
            case XmlNodeType.CDATA:
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                writer.writeCharacters(reader.Value);
                break;
            case XmlNodeType.Comment:
                writer.writeComment(reader.Value);
                break;
            case XmlNodeType.ProcessingInstruction:
                writer.writeProcessingInstruction(reader.Name, reader.Value);
                break;
            case XmlNodeType.DocumentType:
            case XmlNodeType.EntityReference:
                // DtdProcessing.Prohibit ile oluşamaz; oluşursa fail-closed.
                throw new XmlException("DTD / entity referansı desteklenmiyor.");
        }
    }

    private static void CopyAttributes(XmlReader reader, BuildingStreamWriter writer)
    {
        if (!reader.MoveToFirstAttribute())
            return;

        do
        {
            if (reader.NamespaceURI == XmlnsNamespace)
            {
                if (reader.Prefix == "xmlns")
                    writer.writeNamespace(reader.LocalName, reader.Value);
                else
                    writer.writeDefaultNamespace(reader.Value);
            }
            else
            {
                writer.writeAttribute(reader.Prefix, reader.NamespaceURI, reader.LocalName, reader.Value);
            }
        }
        while (reader.MoveToNextAttribute());

        reader.MoveToElement();
    }
}
