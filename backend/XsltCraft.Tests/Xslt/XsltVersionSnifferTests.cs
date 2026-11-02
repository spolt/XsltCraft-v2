using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Tests.Xslt;

public sealed class XsltVersionSnifferTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    private static string Stylesheet(string version, string root = "stylesheet") =>
        $"""<xsl:{root} version="{version}" xmlns:xsl="{XslNs}"><xsl:template match="/"/></xsl:{root}>""";

    [Fact]
    public void Sniff_1_0() => Assert.Equal(1.0m, XsltVersionSniffer.Sniff(Stylesheet("1.0")));

    [Fact]
    public void Sniff_2_0() => Assert.Equal(2.0m, XsltVersionSniffer.Sniff(Stylesheet("2.0")));

    [Fact]
    public void Sniff_3_0() => Assert.Equal(3.0m, XsltVersionSniffer.Sniff(Stylesheet("3.0")));

    [Fact]
    public void Sniff_Transform_Root() => Assert.Equal(2.0m, XsltVersionSniffer.Sniff(Stylesheet("2.0", "transform")));

    [Fact]
    public void Sniff_Missing() =>
        Assert.Null(XsltVersionSniffer.Sniff($"""<xsl:stylesheet xmlns:xsl="{XslNs}"/>"""));

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<unclosed")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Sniff_Garbage(string? input) => Assert.Null(XsltVersionSniffer.Sniff(input));

    [Theory]
    [InlineData("abc")]
    [InlineData("1,0")]
    [InlineData("-2.0")]
    [InlineData("2.0e0")]
    public void Sniff_NonNumericVersion_IsNull(string version) =>
        Assert.Null(XsltVersionSniffer.Sniff(Stylesheet(version)));

    [Fact]
    public void Sniff_VersionWithWhitespace_Trimmed() =>
        Assert.Equal(2.0m, XsltVersionSniffer.Sniff(Stylesheet(" 2.0 ")));

    [Fact]
    public void Sniff_Bom_XmlDeclaration_Comment_Pi_Tolerated()
    {
        var xslt = "﻿<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- yorum -->\n<?pi veri?>\n" + Stylesheet("2.0");

        Assert.Equal(2.0m, XsltVersionSniffer.Sniff(xslt));
    }

    [Fact]
    public void Sniff_Simplified_LiteralResultElement()
    {
        var xslt = $"""<html xsl:version="2.0" xmlns:xsl="{XslNs}"><body><xsl:value-of select="."/></body></html>""";

        Assert.Equal(2.0m, XsltVersionSniffer.Sniff(xslt));
    }

    [Fact]
    public void Sniff_Simplified_UnprefixedVersionAttribute_Ignored()
    {
        // LRE'de ad alanısız "version" bir çıktı özniteliğidir, XSLT sürümü değil.
        var xslt = $"""<html version="2.0" xmlns:xsl="{XslNs}"/>""";

        Assert.Null(XsltVersionSniffer.Sniff(xslt));
    }

    [Fact]
    public void Sniff_NonStylesheetXslRoot_IsNull() =>
        Assert.Null(XsltVersionSniffer.Sniff($"""<xsl:template version="2.0" match="/" xmlns:xsl="{XslNs}"/>"""));

    [Fact]
    public void Sniff_WrongNamespace_IsNull() =>
        Assert.Null(XsltVersionSniffer.Sniff("""<xsl:stylesheet version="2.0" xmlns:xsl="urn:not-xslt"/>"""));

    [Fact]
    public void Sniff_Dtd_Rejected()
    {
        var xslt = $"""
            <?xml version="1.0"?>
            <!DOCTYPE xsl:stylesheet [<!ENTITY v "2.0">]>
            <xsl:stylesheet version="&v;" xmlns:xsl="{XslNs}"/>
            """;

        Assert.Null(XsltVersionSniffer.Sniff(xslt));
    }

    [Fact]
    public void Sniff_ReadsOnlyRoot_BrokenBodyIgnored()
    {
        // Kök etiketten sonrası okunmaz: gövdedeki bozukluk sürüm tespitini etkilemez.
        var xslt = $"""<xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}"><xsl:template></broken>""";

        Assert.Equal(2.0m, XsltVersionSniffer.Sniff(xslt));
    }
}
