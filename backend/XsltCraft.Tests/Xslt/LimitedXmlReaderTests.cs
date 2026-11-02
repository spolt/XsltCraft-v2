using System.Xml;

using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Tests.Xslt;

/// <summary>Tek geçişli girdi sınırlayıcı (eski XmlShapeGuard'ın yerine; G1 reviewer önerisi).</summary>
public sealed class LimitedXmlReaderTests
{
    private static LimitedXmlReader Create(string xml, int maxDepth = 64, long maxNodes = 1000) =>
        new(XmlReader.Create(new StringReader(xml), SecureXslt.ReaderSettings()), maxDepth, maxNodes, XsltFailureKind.Xml, XsltEngineKind.DotNet);

    private static void ReadAll(XmlReader r)
    {
        while (r.Read())
        {
        }
    }

    private static string Deep(int n) => string.Concat(Enumerable.Repeat("<a>", n)) + string.Concat(Enumerable.Repeat("</a>", n));

    [Fact]
    public void Depth_Boundary_IsInclusive()
    {
        using (var ok = Create(Deep(5), maxDepth: 5))
            ReadAll(ok);

        using var tooDeep = Create(Deep(6), maxDepth: 5);
        var ex = Assert.Throws<XsltProcessingException>(() => ReadAll(tooDeep));
        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
        Assert.Equal(XsltEngineKind.DotNet, ex.Diagnostic.Engine);
        Assert.Equal(1, ex.Diagnostic.Line);
        Assert.True(ex.Diagnostic.Column > 0);
    }

    [Fact]
    public void Nodes_CountAttributes()
    {
        // <r a b c/> = 1 eleman + 3 öznitelik = 4 düğüm.
        using (var ok = Create("<r a=\"1\" b=\"2\" c=\"3\"/>", maxNodes: 4))
            ReadAll(ok);

        using var over = Create("<r a=\"1\" b=\"2\" c=\"3\"/>", maxNodes: 3);
        Assert.Throws<XsltProcessingException>(() => ReadAll(over));
    }

    [Fact]
    public void Dtd_StillProhibitedByInnerReader()
    {
        using var r = Create("<!DOCTYPE r [<!ENTITY e \"x\">]><r>&e;</r>");
        Assert.Throws<XmlException>(() => ReadAll(r));
    }

    [Fact]
    public void XmlDocumentLoad_SameAsPlainReader()
    {
        const string xml = "<r xmlns:p=\"urn:p\" xml:space=\"preserve\">\n  <p:a x=\"1\">t</p:a> <!--c--><?pi v?><![CDATA[<x>]]></r>";
        var plain = new XmlDocument { XmlResolver = null };
        using (var r = XmlReader.Create(new StringReader(xml), SecureXslt.ReaderSettings()))
            plain.Load(r);

        var limited = new XmlDocument { XmlResolver = null };
        using (var r = Create(xml))
            limited.Load(r);

        Assert.Equal(plain.OuterXml, limited.OuterXml);
        Assert.Equal(plain.DocumentElement!.ChildNodes.Count, limited.DocumentElement!.ChildNodes.Count);
    }

    [Fact]
    public void StylesheetDepth_SingleCompilePass()
    {
        var body = Deep(20);
        var xslt = $"<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:template match=\"/\">{body}</xsl:template></xsl:stylesheet>";

        Assert.NotNull(SecureXslt.CompileWithDepthLimit(xslt, 64));
        var ex = Assert.ThrowsAny<Exception>(() => SecureXslt.CompileWithDepthLimit(xslt, 10));
        var xpe = ex as XsltProcessingException ?? XsltProcessingException.FindIn(ex);
        Assert.NotNull(xpe);
        Assert.Equal(XsltFailureKind.LimitExceeded, xpe!.Diagnostic.Kind);
    }
}
