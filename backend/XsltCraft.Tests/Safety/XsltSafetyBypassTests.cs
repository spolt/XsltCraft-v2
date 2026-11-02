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

    // ── G1 düzeltmesi: AVT sınırı (yorum / iç içe süslü parantez / braced URI) ───

    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>Kök özniteliği ve üst düzey bildirimlerle tam stylesheet (Wrap'ın kapsamadığı durumlar).</summary>
    private static string Sheet(string rootAttrs, string topLevel, string templateBody = "<p>x</p>", string ns = "") =>
        $"<xsl:stylesheet version=\"2.0\" {rootAttrs} xmlns:xsl=\"{XslNs}\" {ns}>{topLevel}"
        + $"<xsl:template match=\"/\">{templateBody}</xsl:template></xsl:stylesheet>";

    private static void AssertSecurity(string xslt, string code)
    {
        // Security bulgusu her iki modda da raporlanmalı.
        Assert.Contains(code, Codes(xslt, XsltSafetyMode.Execute));
        Assert.Contains(code, Codes(xslt, XsltSafetyMode.Store));
    }

    [Fact]
    public void AvtCommentBrace_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"{(: } :) doc('file:///etc/passwd')}\"/>"), "ForbiddenFunction");

    [Fact]
    public void AvtNestedCommentBrace_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"{1 (: dış (: } :) } :) , doc('file:///etc/passwd')}\"/>"), "ForbiddenFunction");

    [Fact]
    public void AvtUnclosedComment_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"{(: } doc('x')}\"/>"), "MalformedAvt");

    [Fact]
    public void AvtUnclosedBrace_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"x{string(.)\"/>"), "MalformedAvt");

    [Fact]
    public void AvtUnclosedString_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"{'abc}\"/>"), "MalformedAvt");

    [Fact]
    public void AvtMapConstructorBraces_IsThreat() =>
        // İlk '}' map kurucusunu kapatır, ifadeyi değil — doc() ifadenin içinde kalır.
        AssertSecurity(Wrap("<a href=\"{map{'a':1}?a, doc('x')}\"/>"), "ForbiddenFunction");

    [Fact]
    public void AvtBracedUriWithQuote_IsAmbiguous_IsThreat() =>
        AssertSecurity(Wrap("<a href=\"{Q{'}f(), doc('x')}\"/>"), "MalformedAvt");

    [Fact]
    public void PrefixedXmlnsLocalName_IsScannedAsAvt() =>
        // Ad alanı bildirimi yalnız xmlns ad alanıdır; "p:xmlns" sıradan LRE (AVT) özniteliğidir.
        AssertSecurity(Wrap("<a p:xmlns=\"{doc('x')}\" xmlns:p=\"urn:p\"/>"), "ForbiddenFunction");

    [Theory]
    [InlineData("<xsl:value-of select=\"'abc\"/>")]
    [InlineData("<xsl:value-of select=\"1 (: kapanmayan\"/>")]
    [InlineData("<xsl:value-of select=\"Q{urn:x\"/>")]
    public void MalformedXPath_IsThreat(string body) =>
        AssertSecurity(Wrap(body), "MalformedXPath");

    // ── G1 düzeltmesi: collation AVT ──────────────────────────────────────────

    [Fact]
    public void SortCollationAvt_Doc_IsThreat()
    {
        var xslt = Wrap("<xsl:for-each select=\"*\"><xsl:sort select=\".\" collation=\"{doc('file:///x')}\"/></xsl:for-each>");
        AssertSecurity(xslt, "ForbiddenFunction");
        AssertSecurity(xslt, "UnsafeCollation");
    }

    [Theory]
    [InlineData("{concat('http://saxon', '.sf.net/collation?class=X')}")]
    [InlineData("http://www.w3.org/2005/xpath-functions/collation/{'codepoint'}")]
    public void DynamicCollation_IsThreat(string collation) =>
        AssertSecurity(
            Wrap($"<xsl:for-each select=\"*\"><xsl:sort select=\".\" collation=\"{collation}\"/></xsl:for-each>"),
            "UnsafeCollation");

    [Fact]
    public void StaticCodepointCollation_NoFindings() =>
        Assert.Empty(Codes(Wrap("<xsl:for-each select=\"*\"><xsl:sort select=\".\" "
            + "collation=\"http://www.w3.org/2005/xpath-functions/collation/codepoint\"/></xsl:for-each>")));

    // ── G1 düzeltmesi: xsl:function ile yasak ad alanı beyaz listeye girmez ─────

    private static string WithFunction(string fnName, string nsDecl, string call) =>
        Sheet("", $"<xsl:function name=\"{fnName}\"><xsl:sequence select=\"1\"/></xsl:function>",
            $"<xsl:value-of select=\"{call}\"/>", nsDecl);

    [Fact]
    public void UserFunctionInSaxonNs_DoesNotWhitelist()
    {
        var xslt = WithFunction("saxon:x", "xmlns:saxon=\"http://saxon.sf.net/\"", "saxon:evaluate('1')");
        AssertSecurity(xslt, "ForbiddenFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenNamespace"); // tanımın kendisi
    }

    [Fact]
    public void UserFunctionInJavaNs_DoesNotWhitelist()
    {
        var xslt = WithFunction("j:f", "xmlns:j=\"java:java.lang.Runtime\"", "j:getRuntime()");
        AssertSecurity(xslt, "ForbiddenFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenNamespace");
    }

    [Fact]
    public void UserFunctionInClitypeNs_DoesNotWhitelist()
    {
        var xslt = WithFunction("c:f", "xmlns:c=\"clitype:System.IO.File\"", "c:ReadAllText('x')");
        AssertSecurity(xslt, "ForbiddenFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenNamespace");
    }

    [Fact]
    public void UserFunctionEqNameInJavaNs_IsThreat() =>
        AssertSecurity(Sheet("", "<xsl:function name=\"Q{java:java.lang.Runtime}f\"><xsl:sequence select=\"1\"/></xsl:function>", "<a/>"),
            "ForbiddenNamespace");

    [Fact]
    public void UserFunctionInMathNs_StillUnsupported()
    {
        var xslt = WithFunction("m:x", "xmlns:m=\"http://www.w3.org/2005/xpath-functions/math\"", "m:pi()");
        Assert.Contains("UnsupportedFunctionNamespace", Codes(xslt, XsltSafetyMode.Execute));
        AssertSecurity(xslt, "ReservedFunctionNamespace");
    }

    [Theory]
    [InlineData("fn", "http://www.w3.org/2005/xpath-functions")]
    [InlineData("xs", "http://www.w3.org/2001/XMLSchema")]
    [InlineData("x", "http://www.w3.org/1999/XSL/Transform")]
    [InlineData("math", "http://www.w3.org/2005/xpath-functions/math")]
    [InlineData("map", "http://www.w3.org/2005/xpath-functions/map")]
    [InlineData("array", "http://www.w3.org/2005/xpath-functions/array")]
    public void UserFunctionNameInReservedNs_IsThreat(string prefix, string uri) =>
        AssertSecurity(
            Sheet("", $"<xsl:function name=\"{prefix}:f\"><xsl:sequence select=\"1\"/></xsl:function>", "<a/>",
                $"xmlns:{prefix}=\"{uri}\""),
            "ReservedFunctionNamespace");

    [Fact]
    public void UserFunctionInOwnNs_StillAllowed() =>
        Assert.Empty(Codes(WithFunction("my:f", "xmlns:my=\"urn:my\"", "my:f()")));

    // ── G1 kapanış: ad alanı URI'si boşluk normalizasyonu (Saxon URI'yi kırpar) ───

    [Fact]
    public void WhitespaceJavaNs_UserFunction_DoesNotWhitelist()
    {
        var xslt = WithFunction("j:x", "xmlns:j=\" java:java.lang.System\"",
            "Q{ java:java.lang.System}getProperty('user.home')");
        AssertSecurity(xslt, "ForbiddenFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenNamespace"); // tanımın kendisi
    }

    [Fact]
    public void WhitespaceSaxonNs_UserFunction_DoesNotWhitelist()
    {
        var xslt = WithFunction("s:x", "xmlns:s=\" http://saxon.sf.net/\"", "Q{ http://saxon.sf.net/}eval('1')");
        AssertSecurity(xslt, "ForbiddenFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenNamespace");
    }

    [Theory]
    [InlineData("f:x", "xmlns:f=\"http://www.w3.org/2005/xpath-functions \"")]
    [InlineData("Q{ http://www.w3.org/2005/xpath-functions}x", "")]
    public void WhitespaceFnNs_UserFunction_IsReserved(string fnName, string nsDecl)
    {
        var xslt = WithFunction(fnName, nsDecl, "Q{http://www.w3.org/2005/xpath-functions }doc('x')");
        AssertSecurity(xslt, "ReservedFunctionNamespace");
        AssertSecurity(xslt, "ForbiddenFunction"); // fn ad alanı kullanıcı kümesine girmedi
    }

    [Fact]
    public void EQNameWhitespaceUri_FnDoc_IsForbidden()
    {
        AssertSecurity(Wrap("<xsl:copy-of select=\"Q{ http://www.w3.org/2005/xpath-functions }doc('x')\"/>"),
            "ForbiddenFunction");
        AssertSecurity(Wrap("<a href=\"{Q{&#9;http://www.w3.org/2005/xpath-functions&#10;}doc('x')}\"/>"),
            "ForbiddenFunction");
    }

    [Fact]
    public void EQNameWhitespaceUri_Java_IsForbidden() =>
        AssertSecurity(Wrap("<xsl:value-of select=\"Q{  java:java.lang.System }getProperty('user.home')\"/>"),
            "ForbiddenFunctionNamespace");

    /// <summary>Normalizasyon sonrası iç boşluk kalan URI kullanıcı fonksiyonu barındıramaz (fail-closed).</summary>
    [Fact]
    public void InnerWhitespaceUri_UserFunction_IsNotWhitelisted() =>
        AssertSecurity(WithFunction("my:f", "xmlns:my=\"urn:a  b\"", "my:f()"), "ForbiddenFunctionNamespace");

    [Fact]
    public void WhitespaceOwnNs_UserFunction_StillAllowed() =>
        Assert.Empty(Codes(WithFunction("my:f", "xmlns:my=\" urn:my \"", "Q{urn:my}f()")));

    [Theory]
    [InlineData(" http://saxon.sf.net/ ", "http://saxon.sf.net/")]
    [InlineData("\t\r\nurn:a \t b\n", "urn:a b")]
    [InlineData("urn:x", "urn:x")]
    [InlineData("   ", "")]
    public void NormalizeNamespaceUri_TrimsAndCollapses(string raw, string expected) =>
        Assert.Equal(expected, XsltSafetyNamespaces.NormalizeNamespaceUri(raw));

    // ── G1 düzeltmesi: boolean öznitelik (expand-text / static) fail-closed ─────

    [Fact]
    public void ExpandTextWhitespace_IsThreat() =>
        AssertSecurity(Sheet("expand-text=\" yes\"", "", "<p>{doc('file:///etc/passwd')}</p>"), "ExpandText");

    [Fact]
    public void ExpandTextWhitespace_OnLre_IsThreat() =>
        AssertSecurity(Wrap("<p xsl:expand-text=\" yes\">{doc('x')}</p>"), "ExpandText");

    [Fact]
    public void StaticWhitespace_IsThreat() =>
        AssertSecurity(Sheet("", "<xsl:param name=\"p\" static=\" yes\" select=\"1\"/>"), "StaticVariable");

    [Theory]
    [InlineData("YES")]
    [InlineData("Yes")]
    [InlineData("1")]
    [InlineData("true ")]
    [InlineData("TRUE")]
    [InlineData("")]
    [InlineData("on")]
    [InlineData(" no")] // NBSP XML boşluğu değil → "no" sayılmaz
    public void ExpandTextOddValue_IsThreat(string value) =>
        AssertSecurity(Sheet($"expand-text=\"{value}\"", ""), "ExpandText");

    [Theory]
    [InlineData("no")]
    [InlineData(" no ")]
    [InlineData("NO")]
    [InlineData("false")]
    [InlineData("0")]
    public void ExpandTextExplicitOff_NoFindings(string value)
    {
        Assert.Empty(Codes(Sheet($"expand-text=\"{value}\"", "")));
        Assert.Empty(Codes(Sheet("", $"<xsl:param name=\"p\" static=\"{value}\" select=\"1\"/>")));
    }

    // ── Store modu yanlış-pozitif / izin listesi ───────────────────────────────

    private const string MsxslDecl = "xmlns:ms=\"urn:schemas-microsoft-com:xslt\"";
    private const string ExslDecl = "xmlns:exsl=\"http://exslt.org/common\"";

    [Theory]
    [InlineData("ms:node-set($v)")]
    [InlineData("ms:format-date('2020-01-01', 'yyyy')")]
    [InlineData("ms:format-time('10:00:00', 'HH')")]
    [InlineData("ms:string-compare('a', 'b')")]
    [InlineData("ms:utc('2020-01-01')")]
    [InlineData("ms:local-name('a')")]
    [InlineData("ms:namespace-uri('a')")]
    [InlineData("ms:number('1')")]
    [InlineData("exsl:node-set($v)")]
    [InlineData("exsl:object-type($v)")]
    public void HarmlessMsxslExslFunctions_NoFindings(string call)
    {
        var xslt = Sheet("", "<xsl:variable name=\"v\"><a/></xsl:variable>",
            $"<xsl:value-of select=\"string({call})\"/>", MsxslDecl + " " + ExslDecl);
        Assert.Empty(Codes(xslt, XsltSafetyMode.Store));
        Assert.Empty(Codes(xslt, XsltSafetyMode.Execute));
    }

    [Theory]
    [InlineData("ms:foo()")]
    [InlineData("exsl:document('x')")]
    public void UnknownMsxslExslFunctions_AreThreat(string call) =>
        AssertSecurity(Sheet("", "", $"<xsl:value-of select=\"{call}\"/>", MsxslDecl + " " + ExslDecl),
            "ForbiddenFunctionNamespace");

    [Theory]
    [InlineData("xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\"")]
    [InlineData("")] // bildirilmemiş msxsl öneki: çalışma anı hatası, zararsız
    public void SystemPropertyMsxslLiteral_NoFindings(string nsDecl) =>
        Assert.Empty(Codes(Sheet("", "", "<xsl:value-of select=\"system-property('msxsl:version')\"/>", nsDecl),
            XsltSafetyMode.Store));

    [Theory]
    [InlineData("system-property(concat('msxsl:', 'version'))")] // dinamik argüman
    [InlineData("system-property('xsl:' || 'vendor')")]          // literal + birleştirme
    [InlineData("system-property(' xsl:vendor')")]               // boşluklu ad
    [InlineData("system-property('java.version')")]              // öneksiz (Java sistem özelliği)
    public void SystemPropertyNonLiteralOrForeign_IsThreat(string expr) =>
        AssertSecurity(Wrap($"<xsl:value-of select=\"{expr}\"/>"), "SystemProperty");

    [Fact]
    public void SystemPropertyXslPrefixReboundToOtherNs_IsThreat() =>
        AssertSecurity(
            Wrap("<x:value-of xmlns:x=\"http://www.w3.org/1999/XSL/Transform\" xmlns:xsl=\"urn:other\" "
                + "select=\"system-property('xsl:vendor')\"/>"),
            "SystemProperty");

    [Theory]
    [InlineData("<a _foo=\"bar\">x</a>")]
    [InlineData("<a _foo=\"{string(.)}\">x</a>")]
    public void LreUnprefixedUnderscoreAttribute_IsNotShadow(string body) =>
        // XSLT 3.0 §3.13.2: LRE'de gölge öznitelik yalnız xsl: ad alanında (xsl:_ad); "_foo" sıradan çıktı özniteliği.
        Assert.Empty(Codes(Wrap(body), XsltSafetyMode.Store));

    [Fact]
    public void LreUnprefixedUnderscoreAttribute_AvtStillScanned() =>
        AssertSecurity(Wrap("<a _foo=\"{doc('x')}\">x</a>"), "ForbiddenFunction");

    [Fact]
    public void LreXslShadowAttribute_IsThreat() =>
        AssertSecurity(Wrap("<a xsl:_use-when=\"doc-available('x')\">x</a>"), "ShadowAttribute");

    [Fact]
    public void XslNamespacedUseWhen_OnXslElement_IsThreat() =>
        AssertSecurity(Wrap("<xsl:value-of xsl:use-when=\"doc-available('x')\" select=\"1\"/>"), "UseWhen");

    // ── Derinlik: en fazla 256 seviye ─────────────────────────────────────────

    /// <summary>Toplam <paramref name="levels"/> eleman seviyesi (stylesheet + template + iç içe a'lar).</summary>
    private static string NestedSheet(int levels)
    {
        int inner = levels - 2;
        return Wrap(string.Concat(Enumerable.Repeat("<a>", inner)) + string.Concat(Enumerable.Repeat("</a>", inner)));
    }

    [Fact]
    public void Depth256_Allowed() =>
        Assert.DoesNotContain("StylesheetDepthOver256", Codes(NestedSheet(256)));

    [Fact]
    public void Depth257_IsThreat() =>
        AssertSecurity(NestedSheet(257), "StylesheetDepthOver256");

    // ── Sürüm ayrıştırma: sniffer ile aynı kural (XsltVersionNumber) ──────────

    [Theory]
    [InlineData("2.0", false)]
    [InlineData(" 2.0 ", false)]
    [InlineData("3.0", true)]
    [InlineData(" 3.0 ", true)]
    public void Version_ParsedLikeSniffer(string version, bool above20)
    {
        var xslt = $"<xsl:stylesheet version=\"{version}\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"/></xsl:stylesheet>";
        Assert.Equal(above20, XsltCraft.Application.Xslt.Engines.XsltVersionSniffer.Sniff(xslt) > 2.0m);
        Assert.Equal(above20, Codes(xslt).Contains("VersionAbove20"));
    }

    [Theory]
    [InlineData("+3.0")]
    [InlineData("3,0")]
    [InlineData("abc")]
    [InlineData("2.0e0")]
    public void UnparseableVersion_UnsupportedInExecute_AllowedInStore(string version)
    {
        var xslt = $"<xsl:stylesheet version=\"{version}\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"/></xsl:stylesheet>";
        Assert.Null(XsltCraft.Application.Xslt.Engines.XsltVersionSniffer.Sniff(xslt));
        Assert.Contains("InvalidVersion", Codes(xslt, XsltSafetyMode.Execute));
        Assert.Empty(Codes(xslt, XsltSafetyMode.Store));
    }
}
