using System.Xml;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Stylesheet'in bildirdiği XSLT sürümünü yalnız kök elemanı okuyarak bulur:
/// <c>xsl:stylesheet|xsl:transform/@version</c> veya sadeleştirilmiş stylesheet'te
/// (literal sonuç elemanı) <c>@xsl:version</c>. DTD yasak (<see cref="SecureXslt.ReaderSettings"/>);
/// XML bildirimi, BOM, yorum ve PI atlanır. Okunamaz / sürüm yok / sayı değil → <c>null</c>.
/// </summary>
public static class XsltVersionSniffer
{
    private const string XslNamespace = "http://www.w3.org/1999/XSL/Transform";

    public static decimal? Sniff(string? xslt)
    {
        if (string.IsNullOrWhiteSpace(xslt))
            return null;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xslt.TrimStart('﻿')), SecureXslt.ReaderSettings());
            if (reader.MoveToContent() != XmlNodeType.Element)
                return null;

            return XsltVersionNumber.Parse(ReadVersionAttribute(reader));
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string? ReadVersionAttribute(XmlReader root)
    {
        if (root.NamespaceURI == XslNamespace)
        {
            return root.LocalName is "stylesheet" or "transform"
                ? root.GetAttribute("version")
                : null;
        }

        return root.GetAttribute("version", XslNamespace);
    }
}
