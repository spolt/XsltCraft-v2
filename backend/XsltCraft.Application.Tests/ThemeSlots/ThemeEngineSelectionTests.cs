using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>
/// T-izi motor seçimi: GİB temalarının hepsi <c>version="2.0"</c> bildirir; yalnız 2.0 yapısı kullanan (general.xslt,
/// <c>exists()</c>) Saxon'a gider — diğerleri .NET'te kalır, böylece çıktıları bugünküyle bit-aynıdır.
/// </summary>
public class ThemeEngineSelectionTests
{
    [Theory]
    [InlineData(ThemeFixtures.TestTheme, XsltEngineKind.DotNet)]
    [InlineData(ThemeFixtures.GibEInvoice, XsltEngineKind.DotNet)]
    [InlineData(ThemeFixtures.GibTemplate, XsltEngineKind.DotNet)]
    [InlineData(ThemeFixtures.GibEIrsaliye, XsltEngineKind.DotNet)]
    [InlineData(ThemeFixtures.GibGeneral, XsltEngineKind.Saxon)]
    public void Fixture_themes_route_by_xslt2_usage(string fixture, XsltEngineKind expected)
    {
        var xslt = ThemeFixtures.Load(fixture);
        Assert.Equal(2.0m, XsltVersionSniffer.Sniff(xslt));
        Assert.Equal(expected, XsltEngineSelector.SelectForTheme(XsltEngineMode.Auto, XsltVersionSniffer.Sniff(xslt), xslt));
    }

    [Theory]
    [InlineData("<xsl:value-of select=\"exists(/a)\"/>", true)]
    [InlineData("<xsl:value-of select=\"if (/a) then 1 else 2\"/>", true)]
    [InlineData("<xsl:value-of select=\"string-join(/a/b, ',')\"/>", true)]
    [InlineData("<xsl:value-of select=\"/a/b eq 'x'\"/>", true)]
    [InlineData("<xsl:value-of select=\"for $x in /a return $x\"/>", true)]
    [InlineData("<xsl:value-of select=\"fn:count(/a)\" xmlns:fn=\"http://www.w3.org/2005/xpath-functions\"/>", true)]
    [InlineData("<xsl:for-each-group select=\"/a\" group-by=\".\"/>", true)]
    [InlineData("<p title=\"{upper-case(/a)}\"/>", true)]
    [InlineData("<xsl:value-of select=\"concat(/a, 'exists(x)')\"/>", false)]
    [InlineData("<xsl:value-of select=\"format-number(sum(/a/b) div 2, '#.##')\"/>", false)]
    [InlineData("<xsl:if test=\"not(/a) and (count(/b) &gt; 1 or /c mod 2)\"><xsl:apply-templates select=\"node()|text()\"/></xsl:if>", false)]
    [InlineData("<xsl:value-of select=\"msxsl:node-set($v)\" xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\"/>", false)]
    [InlineData("<script>function f(){ return exists(1); }</script>", false)]
    public void Detects_only_constructs_the_10_engine_cannot_run(string body, bool expected)
    {
        var xslt = $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:character-map name="a"><xsl:output-character character="&#128;" string=""/></xsl:character-map>
              <xsl:output method="html" use-character-maps="a"/>
              <xsl:template match="/">{body}</xsl:template>
            </xsl:stylesheet>
            """;

        Assert.Equal(expected, Xslt2FeatureDetector.UsesXslt2Features(xslt));
    }

    [Fact]
    public void Version_10_and_non_auto_modes_never_pick_saxon_by_features()
    {
        const string xslt = """<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"><xsl:template match="/"><xsl:value-of select="exists(/a)"/></xsl:template></xsl:stylesheet>""";

        Assert.Equal(XsltEngineKind.DotNet, XsltEngineSelector.SelectForTheme(XsltEngineMode.Auto, 1.0m, xslt));
        Assert.Equal(XsltEngineKind.DotNet, XsltEngineSelector.SelectForTheme(XsltEngineMode.DotNet, 2.0m, xslt));
        Assert.Equal(XsltEngineKind.Saxon, XsltEngineSelector.SelectForTheme(XsltEngineMode.Saxon, 1.0m, xslt));
    }
}
