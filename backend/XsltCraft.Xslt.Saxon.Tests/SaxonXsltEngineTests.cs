using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>XSLT 2.0 işlevselliği (spike K2'nin üretim motoruyla tekrarı) ve sözleşme davranışı.</summary>
public sealed class SaxonXsltEngineTests
{
    [Fact]
    public void SaxonXsltEngine_GibGeneral_Renders()
    {
        var html = Render(GibGeneral, InvoiceXml);

        Assert.StartsWith("<!DOCTYPE html", html.TrimStart());
        Assert.Contains("<title>e-Belge</title>", html);
        Assert.Contains("TST2026000000001", html);                    // fatura no ($XML üzerinden)
        Assert.Contains("111d1111-0c11-01bb-123e-1f2f34b567f8", html);  // ETTN
        Assert.Contains("Satış Temsilcisi: Semih Polat", html);        // UTF-8 / Türkçe
        Assert.DoesNotContain("NaN", html);
    }

    [Fact]
    public void SaxonXsltEngine_GibGeneral_Renders_InProcessGridProfile()
    {
        var html = Render(GibGeneral, InvoiceXml, engine: GridEngine);
        Assert.Contains("111d1111-0c11-01bb-123e-1f2f34b567f8", html);
    }

    [Fact]
    public void Exists_Works()
    {
        var html = Render(Stylesheet("<xsl:value-of select=\"exists(/root/item[@g='a'])\"/>,<xsl:value-of select=\"exists(/root/none)\"/>"),
            "<root><item g=\"a\"/></root>");
        Assert.Equal("<out>true,false</out>", html);
    }

    [Fact]
    public void CharacterMap_Applied()
    {
        const string xslt = $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}">
              <xsl:character-map name="c1"><xsl:output-character character="&#150;" string=""/></xsl:character-map>
              <xsl:output method="xml" omit-xml-declaration="yes" use-character-maps="c1"/>
              <xsl:template match="/"><cm>A&#150;B<xsl:value-of select="/r"/></cm></xsl:template>
            </xsl:stylesheet>
            """;
        var html = Render(xslt, "<r>C&#150;D</r>");
        Assert.Equal("<cm>ABCD</cm>", html);
    }

    [Fact]
    public void KeyGenerateId_Grouping()
    {
        var xslt = Stylesheet(
            "<xsl:for-each select=\"/root/item[generate-id() = generate-id(key('k', @g)[1])]\"><g n=\"{@g}\" c=\"{count(key('k', @g))}\"/></xsl:for-each>",
            top: "<xsl:key name=\"k\" match=\"item\" use=\"@g\"/>");
        var html = Render(xslt, "<root><item g=\"a\"/><item g=\"b\"/><item g=\"a\"/></root>");
        Assert.Equal("<out><g n=\"a\" c=\"2\"/><g n=\"b\" c=\"1\"/></out>", html);
    }

    [Fact]
    public void DecimalFormat_NaNEmpty()
    {
        var xslt = Stylesheet(
            "[<xsl:value-of select=\"format-number(number('abc'), '#.##0,00', 'eu')\"/>]<xsl:value-of select=\"format-number(1234.5, '#.##0,00', 'eu')\"/>",
            top: "<xsl:decimal-format name=\"eu\" decimal-separator=\",\" grouping-separator=\".\" NaN=\"\"/>");
        Assert.Equal("<out>[]1.234,50</out>", Render(xslt));
    }

    [Fact]
    public void ParamDefault_Used()
    {
        var xslt = Stylesheet("<xsl:value-of select=\"$P\"/>", top: "<xsl:param name=\"P\" select=\"'varsayilan'\"/>");
        Assert.Equal("<out>varsayilan</out>", Render(xslt));
    }

    [Fact]
    public void GlobalVariableRoot_Works()
    {
        // applyTemplates global bağlam öğesini kurmaz → setGlobalContextItem şart (aksi XPDY0002).
        var xslt = Stylesheet("<xsl:for-each select=\"$XML\"><xsl:value-of select=\"count(//item)\"/></xsl:for-each>",
            top: "<xsl:variable name=\"XML\" select=\"/\"/>");
        Assert.Equal("<out>3</out>", Render(xslt, "<root><item/><item/><item/></root>"));
    }

    [Fact]
    public void ForEachGroup_Works()
    {
        var xslt = Stylesheet("<xsl:for-each-group select=\"//i\" group-by=\"@k\"><g k=\"{current-grouping-key()}\" n=\"{count(current-group())}\"/></xsl:for-each-group>");
        Assert.Equal("<out><g k=\"x\" n=\"2\"/><g k=\"y\" n=\"1\"/></out>", Render(xslt, "<r><i k=\"x\"/><i k=\"y\"/><i k=\"x\"/></r>"));
    }

    [Fact]
    public void StripSpace_AppliedToBridgedInput()
    {
        // Ağacı kendimiz kurduğumuz için xsl:strip-space politikası köprüye açıkça verilmeli.
        const string xml = "<r>\n  <a>x</a>\n  <b>y</b>\n</r>";
        var strip = Stylesheet("<xsl:value-of select=\"count(/r/node())\"/>", top: "<xsl:strip-space elements=\"*\"/>");
        var keep = Stylesheet("<xsl:value-of select=\"count(/r/node())\"/>");

        Assert.Equal("<out>2</out>", Render(strip, xml));
        Assert.Equal("<out>5</out>", Render(keep, xml));
    }

    [Fact]
    public void Compiled_IsReusable_AcrossTransforms()
    {
        var compiled = Engine.Compile(Stylesheet("<xsl:value-of select=\"/r\"/>"), XsltExecutionLimits.Default);
        for (var i = 0; i < 3; i++)
        {
            var sw = new StringWriter();
            var timings = compiled.Transform($"<r>{i}</r>", sw, XsltExecutionLimits.Default);
            Assert.Equal($"<out>{i}</out>", sw.ToString());
            Assert.Equal(0, timings.CompileMs);
        }

        Assert.Equal(XsltEngineKind.Saxon, compiled.Kind);
    }

    [Fact]
    public void ParallelRenders_DistinctInputs_Isolated()
    {
        var compiled = Engine.Compile(Stylesheet("<xsl:value-of select=\"sum(//n)\"/>"), XsltExecutionLimits.Default);
        var results = Enumerable.Range(1, 16).AsParallel().Select(i =>
        {
            var sw = new StringWriter();
            compiled.Transform($"<r><n>{i}</n><n>{i}</n></r>", sw, XsltExecutionLimits.Default);
            return (i, sw.ToString());
        }).ToList();

        Assert.All(results, r => Assert.Equal($"<out>{r.i * 2}</out>", r.Item2));
    }

    [Fact]
    public void SaxonVersion_Is12_10()
    {
        Assert.Equal("12.10", Engine.SaxonVersion);
        Assert.Equal(XsltEngineKind.Saxon, Engine.Kind);
    }

    [Fact]
    public void Output_OverLimit_OutputLimit()
    {
        var limits = XsltExecutionLimits.Default with { MaxOutputChars = 100 };
        var d = Fail(Stylesheet("<xsl:for-each select=\"1 to 1000\"><x/></xsl:for-each>"), limits: limits);
        Assert.Equal(XsltFailureKind.OutputLimit, d.Kind);
    }

    [Fact]
    public void InputXml_TooDeep_Xml()
    {
        var limits = XsltExecutionLimits.Default with { MaxInputDepth = 10 };
        var d = Fail(Stylesheet("x"), Deep("a", 11), limits);
        Assert.Equal(XsltFailureKind.Xml, d.Kind);
        Assert.True(d.Line > 0);
    }

    [Fact]
    public void InputXml_TooManyNodes_Xml()
    {
        var limits = XsltExecutionLimits.Default with { MaxInputNodes = 50 };
        var d = Fail(Stylesheet("x"), "<r>" + string.Concat(Enumerable.Repeat("<i/>", 100)) + "</r>", limits);
        Assert.Equal(XsltFailureKind.Xml, d.Kind);
    }

    [Fact]
    public void InputXml_WithDtd_Xml()
    {
        var d = Fail(Stylesheet("x"), """<!DOCTYPE r [<!ENTITY e SYSTEM "file:///c:/windows/win.ini">]><r>&e;</r>""");
        Assert.Equal(XsltFailureKind.Xml, d.Kind);
    }

    [Fact]
    public void StylesheetTooDeep_LimitExceeded()
    {
        var limits = XsltExecutionLimits.Default with { MaxStylesheetDepth = 10 };
        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(Stylesheet(Deep("d", 20)), limits)).Diagnostic;
        Assert.Equal(XsltFailureKind.LimitExceeded, d.Kind);
    }

    [Fact]
    public void MalformedStylesheet_Compile_WithLine()
    {
        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile("<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"" + XslNs + "\">\n<x>", XsltExecutionLimits.Default)).Diagnostic;
        Assert.Equal(XsltFailureKind.Compile, d.Kind);
        Assert.True(d.Line > 0);
    }

    [Fact]
    public void RuntimeError_Runtime_WithCode()
    {
        var d = Fail(Stylesheet("<xsl:value-of select=\"error(QName('urn:x','BOOM'), 'patladı')\"/>"));
        Assert.Equal(XsltFailureKind.Runtime, d.Kind);
        Assert.Equal("BOOM", d.ErrorCode);
        Assert.Contains("patladı", d.Message);
    }
}
