using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Tests;

/// <summary>
/// XsltSafety cephesi (<see cref="XsltSafety.FindThreat"/> = Store modu) — fail-closed, izin listesi.
/// Girdiler <see cref="Stylesheet"/> ile tam, iyi biçimli bir stylesheet'e sarılır; böylece bulgu
/// "iyi biçimli değil" yerine beklenen güvenlik kuralından gelir (yanlış nedenle geçmez).
/// </summary>
public class XsltSafetyTests
{
    /// <summary>Gövdeyi geçerli bir XSLT 2.0 stylesheet'e sarar (kök + /-eşleşen şablon).</summary>
    internal static string Stylesheet(string body) =>
        "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" "
        + "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" "
        + "xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" "
        + "xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">"
        + "<xsl:template match=\"/\">" + body + "</xsl:template></xsl:stylesheet>";

    private static string Code(string xslt) =>
        XsltSafety.Scan(xslt, XsltSafetyMode.Store).Findings[0].Code;

    [Theory]
    [InlineData("<xsl:value-of select=\"document('http://x/')\"/>")]
    [InlineData("<xsl:value-of select=\"doc('http://x/')\"/>")]
    [InlineData("<xsl:value-of select=\"unparsed-text('file:///etc/passwd')\"/>")]
    [InlineData("<xsl:value-of select=\"collection('file:///')\"/>")]
    [InlineData("<xsl:if test=\"doc-available('http://x/')\"><a/></xsl:if>")]
    public void FindThreat_ForbiddenFunctionInSelect_Detected(string body)
    {
        var xslt = Stylesheet(body);
        Assert.NotNull(XsltSafety.FindThreat(xslt));
        Assert.Equal("ForbiddenFunction", Code(xslt));
    }

    [Fact]
    public void FindThreat_SystemPropertyUserDir_Detected()
    {
        var xslt = Stylesheet("<xsl:value-of select=\"system-property('user.dir')\"/>");
        Assert.Equal("SystemProperty", Code(xslt));
    }

    [Fact]
    public void FindThreat_SystemPropertyXslVendor_Allowed()
    {
        var xslt = Stylesheet("<xsl:value-of select=\"system-property('xsl:vendor')\"/>");
        Assert.Null(XsltSafety.FindThreat(xslt));
    }

    [Theory]
    [InlineData("<xsl:import href=\"http://attacker/x.xsl\"/>", "ForbiddenXslElement")]
    [InlineData("<xsl:include href=\"/etc/passwd\"/>", "ForbiddenXslElement")]
    public void FindThreat_ExternalImport_Detected(string decl, string expected)
    {
        // import/include kök seviyesinde olmalı; şablon içine koymak yerine sarmalayıcıyı genişlet.
        var xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
                   + decl + "<xsl:template match=\"/\"><a/></xsl:template></xsl:stylesheet>";
        Assert.NotNull(XsltSafety.FindThreat(xslt));
        Assert.Equal(expected, Code(xslt));
    }

    [Fact]
    public void FindThreat_MalformedXml_IsThreat_FailClosed()
    {
        // İyi biçimli olmayan girdi (kapatılmamış etiket) güvenli kabul edilmez.
        const string xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
                            + "<xsl:template match=\"/\"><a>";
        var report = XsltSafety.Scan(xslt, XsltSafetyMode.Store);
        Assert.NotNull(XsltSafety.FindThreat(xslt));
        Assert.Equal("MalformedXml", report.Findings[0].Code);
    }

    [Fact]
    public void FindThreat_NotAStylesheet_IsThreat()
    {
        // Kök xsl:stylesheet değil ve xsl:version yok.
        const string xslt = "<html><body>merhaba</body></html>";
        Assert.Equal("NotAStylesheet", Code(xslt));
    }

    [Theory]
    [InlineData("<xsl:value-of select=\"//cbc:Note\"/>")]
    [InlineData("<xsl:value-of select=\"format-number(number(//cbc:PayableAmount), '#,##0.00')\"/>")]
    [InlineData("<xsl:for-each select=\"//cac:InvoiceLine\"><xsl:value-of select=\"cbc:ID\"/></xsl:for-each>")]
    [InlineData("<xsl:value-of select=\"'document('')'\"/>")]          // string literal içindeki document( masum
    [InlineData("<xsl:value-of select=\"xs:string(1)\"/>")]            // xs: constructor serbest
    [InlineData("<!-- doc(test) --><a>Belge dokümanı document hazır</a>")]  // yorum/metin yanlış-pozitif değil
    [InlineData("<a as-x=\"xs:string\"/>")]                            // LRE özniteliği AVT, braces yok → serbest
    public void FindThreat_BenignContent_Null(string body)
    {
        Assert.Null(XsltSafety.FindThreat(Stylesheet(body)));
    }

    [Fact]
    public void FindThreat_NullOrEmpty_Null()
    {
        Assert.Null(XsltSafety.FindThreat(null));
        Assert.Null(XsltSafety.FindThreat(""));
    }

    [Fact]
    public void Store_IgnoresUnsupported_ButExecuteBlocks()
    {
        // xsl:iterate (3.0) → Unsupported: Store'da serbest, Execute'ta engel.
        var xslt = Stylesheet("<xsl:iterate select=\"1\"><a/></xsl:iterate>");
        Assert.Null(XsltSafety.FindThreat(xslt));
        Assert.False(XsltSafety.Scan(xslt, XsltSafetyMode.Store).HasFindings);

        var exec = XsltSafety.Scan(xslt, XsltSafetyMode.Execute);
        Assert.True(exec.HasFindings);
        Assert.Equal("UnsupportedXslElement", exec.Findings[0].Code);
        Assert.Equal(XsltSafetySeverity.Unsupported, exec.Findings[0].Severity);
    }
}
