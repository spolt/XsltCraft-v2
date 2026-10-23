using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Tests.Safety;

/// <summary>
/// Yasakçı tarayıcının bilinen atlatma (bypass) vektörlerini yakaladığını ve masum içerikte
/// yanlış-pozitif üretmediğini doğrular. Gerçek vektörler: g0probe/Matrix.cs, Crash.cs,
/// spike-saxon-result.md (security-reviewer Y-3 listesi).
/// </summary>
public class XsltSafetyBypassTests
{
    private static string Wrap(string body) => XsltSafetyTests.Stylesheet(body);

    private static HashSet<string> Codes(string xslt, XsltSafetyMode mode = XsltSafetyMode.Execute) =>
        XsltSafety.Scan(xslt, mode).Findings.Select(f => f.Code).ToHashSet();

    [Theory]
    // Tek tırnaklı string literal argümanı
    [InlineData("<xsl:value-of select=\"doc('file:///C:/x')\"/>", "ForbiddenFunction")]
    // Karakter referansıyla gizleme: &#100;oc( → doc(
    [InlineData("<xsl:value-of select=\"&#100;oc('x')\"/>", "ForbiddenFunction")]
    // AVT içinde
    [InlineData("<a href=\"{doc('x')}\"/>", "ForbiddenFunction")]
    // function-lookup
    [InlineData("<xsl:copy-of select=\"function-lookup(QName('http://www.w3.org/2005/xpath-functions','doc'),1)('x')\"/>", "ForbiddenFunction")]
    // function-lookup + string birleştirme ('trans'||'form')
    [InlineData("<xsl:value-of select=\"function-lookup(QName('http://www.w3.org/2005/xpath-functions','trans'||'form'),2)\"/>", "ForbiddenFunction")]
    // İsimli fonksiyon referansı doc#1
    [InlineData("<xsl:value-of select=\"doc#1\"/>", "NamedFunctionReference")]
    // EQName Q{fn}doc
    [InlineData("<xsl:copy-of select=\"Q{http://www.w3.org/2005/xpath-functions}doc('x')\"/>", "ForbiddenFunction")]
    // EQName Q{fn}transform
    [InlineData("<xsl:copy-of select=\"Q{http://www.w3.org/2005/xpath-functions}transform(.)\"/>", "ForbiddenFunction")]
    // parse-xml
    [InlineData("<xsl:copy-of select=\"parse-xml('&lt;a/&gt;')\"/>", "ForbiddenFunction")]
    // parse-xml-fragment
    [InlineData("<xsl:copy-of select=\"parse-xml-fragment('&lt;a/&gt;')\"/>", "ForbiddenFunction")]
    // Dinamik fonksiyon çağrısı
    [InlineData("<xsl:value-of select=\"$f(1)\"/>", "DynamicFunctionCall")]
    // Satır içi fonksiyon (yorumla gizlenmiş)
    [InlineData("<xsl:value-of select=\"function(: c :)($x){1}\"/>", "InlineFunction")]
    // Gölge öznitelik _select
    [InlineData("<xsl:value-of _select=\"doc('x')\"/>", "ShadowAttribute")]
    // reflexive java:
    [InlineData("<xsl:value-of xmlns:j=\"java:java.lang.System\" select=\"j:getProperty('user.dir')\"/>", "ForbiddenFunctionNamespace")]
    // system-property dışı argüman
    [InlineData("<xsl:value-of select=\"system-property('user.dir')\"/>", "SystemProperty")]
    public void Vectors_AreDetected(string body, string expectedCode)
    {
        Assert.Contains(expectedCode, Codes(Wrap(body)));
    }

    [Fact]
    public void UseWhen_OnLre_IsThreat() =>
        Assert.Contains("UseWhen", Codes(Wrap("<a xsl:use-when=\"exists(doc('x'))\">y</a>")));

    [Fact]
    public void StaticVariable_IsThreat() =>
        Assert.Contains("StaticVariable", Codes(Wrap("<xsl:variable name=\"v\" static=\"yes\" select=\"1\"/>")));

    [Fact]
    public void ExpandText_IsThreat() =>
        Assert.Contains("ExpandText", Codes(Wrap("<a xsl:expand-text=\"yes\">x</a>")));

    [Fact]
    public void ExtensionElementPrefixes_IsThreat()
    {
        const string xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" "
            + "xmlns:ex=\"urn:x\" extension-element-prefixes=\"ex\">"
            + "<xsl:template match=\"/\"><a/></xsl:template></xsl:stylesheet>";
        Assert.Contains("ExtensionElementPrefixes", Codes(xslt));
    }

    [Fact]
    public void SaxonNamespaceElement_IsThreat() =>
        Assert.Contains("ForbiddenNamespace",
            Codes(Wrap("<saxon:while xmlns:saxon=\"http://saxon.sf.net/\" test=\"1\"><a/></saxon:while>")));

    [Fact]
    public void MsxslScript_IsThreat()
    {
        var xslt = Wrap("<msxsl:script xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" language=\"csharp\">x</msxsl:script>");
        Assert.Contains("MsxslScript", Codes(xslt));
    }

    [Fact]
    public void XInclude_IsThreat() =>
        Assert.Contains("XInclude",
            Codes(Wrap("<xi:include xmlns:xi=\"http://www.w3.org/2001/XInclude\" href=\"x\" parse=\"text\"/>")));

    [Fact]
    public void XslTry_IsThreat() =>
        Assert.Contains("ForbiddenXslElement",
            Codes(Wrap("<xsl:try><a/><xsl:catch><b/></xsl:catch></xsl:try>")));

    [Fact]
    public void XslMerge_IsThreat()
    {
        const string body = "<xsl:merge><xsl:merge-source for-each-source=\"'x'\" select=\"/\">"
            + "<xsl:merge-key select=\"string(.)\"/></xsl:merge-source>"
            + "<xsl:merge-action><a/></xsl:merge-action></xsl:merge>";
        Assert.Contains("ForbiddenXslElement", Codes(Wrap(body)));
    }

    [Fact]
    public void SaxonCollationInLiteral_IsThreat() =>
        Assert.Contains("SaxonLiteral",
            Codes(Wrap("<xsl:value-of select=\"compare('a','b','http://saxon.sf.net/collation?class=X')\"/>")));

    [Fact]
    public void PercentCollationInLiteral_IsThreat() =>
        Assert.Contains("UnsafeCollation",
            Codes(Wrap("<xsl:value-of select=\"compare('a','b','http://www.w3.org/2013/collation/UCA?lang=tr%2DTR')\"/>")));

    [Fact]
    public void DefaultCollationAttribute_WithPercent_IsThreat()
    {
        const string xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" "
            + "default-collation=\"http://www.w3.org/2013/collation/UCA?lang=tr%2DTR\">"
            + "<xsl:template match=\"/\"><a/></xsl:template></xsl:stylesheet>";
        Assert.Contains("UnsafeCollation", Codes(xslt));
    }

    [Fact]
    public void Doctype_IsThreat()
    {
        const string xslt = "<!DOCTYPE xsl:stylesheet>"
            + "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
            + "<xsl:template match=\"/\"><a/></xsl:template></xsl:stylesheet>";
        Assert.Contains("Doctype", Codes(xslt));
    }

    [Fact]
    public void BillionLaughs_Doctype_IsThreat()
    {
        const string xslt = "<!DOCTYPE lolz [<!ENTITY a \"aaaa\"><!ENTITY b \"&a;&a;&a;\">]>"
            + "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
            + "<xsl:template match=\"/\"><a>&b;</a></xsl:template></xsl:stylesheet>";
        Assert.Contains("Doctype", Codes(xslt));
    }

    [Fact]
    public void StylesheetDepthOver256_IsThreat()
    {
        var body = string.Concat(Enumerable.Repeat("<a>", 300)) + string.Concat(Enumerable.Repeat("</a>", 300));
        Assert.Contains("StylesheetDepthOver256", Codes(Wrap(body)));
    }

    [Fact]
    public void NestedParensOver64_IsThreat()
    {
        var expr = new string('(', 70) + "1" + new string(')', 70);
        Assert.Contains("NestedParensOver64", Codes(Wrap($"<xsl:value-of select=\"{expr}\"/>")));
    }

    [Fact]
    public void ExpressionOver8KB_IsThreat()
    {
        var expr = "concat('" + new string('x', 9000) + "')";
        Assert.Contains("ExpressionOver8KB", Codes(Wrap($"<xsl:value-of select=\"{expr}\"/>")));
    }

    // ── Unsupported: Execute'ta engel, Store'da serbest ──────────────────────

    [Theory]
    [InlineData("<xsl:iterate select=\"1\"><a/></xsl:iterate>", "UnsupportedXslElement")]
    [InlineData("<xsl:value-of xmlns:math=\"http://www.w3.org/2005/xpath-functions/math\" select=\"math:pi()\"/>", "UnsupportedFunctionNamespace")]
    public void Unsupported_BlockedInExecute_AllowedInStore(string body, string code)
    {
        var xslt = Wrap(body);
        Assert.Contains(code, Codes(xslt, XsltSafetyMode.Execute));
        Assert.DoesNotContain(code, Codes(xslt, XsltSafetyMode.Store));
    }

    [Fact]
    public void VersionAbove20_IsUnsupported()
    {
        const string xslt = "<xsl:stylesheet version=\"3.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
            + "<xsl:template match=\"/\"><a/></xsl:template></xsl:stylesheet>";
        Assert.Contains("VersionAbove20", Codes(xslt, XsltSafetyMode.Execute));
        Assert.DoesNotContain("VersionAbove20", Codes(xslt, XsltSafetyMode.Store));
    }

    // ── Yanlış pozitif olmamalı ──────────────────────────────────────────────

    [Theory]
    [InlineData("<xsl:value-of select=\"'lütfen document() çağır'\"/>")]      // literal içinde document(
    [InlineData("<xsl:output doctype-system=\"about:legacy-compat\" doctype-public=\"-//X\"/>")]  // doctype-* öznitelik URL'si
    [InlineData("<xsl:variable name=\"v\" as=\"xs:string\" select=\"'x'\"/>")]  // as sequence type
    [InlineData("<!-- doc('x') yorumu --><a/>")]                               // yorum
    [InlineData("<xsl:value-of select=\"format-number(number(//cbc:Total), '#,##0.00')\"/>")]  // standart fn
    [InlineData("<a style=\"width:{{10}}px\">x</a>")]                          // {{ }} AVT kaçışı
    public void BenignContent_NoFindings(string body)
    {
        Assert.False(XsltSafety.Scan(Wrap(body), XsltSafetyMode.Execute).HasFindings);
    }
}
