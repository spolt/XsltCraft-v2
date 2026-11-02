using System.Text;
using System.Text.Json;
using System.Xml;

using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Tests.Xslt;

/// <summary>
/// DotNetXsltEngine çıktısı bugünkü önizleme yolu XsltHtmlRenderer ile bit-aynı olmalı;
/// hatalar XsltProcessingException + doğru XsltFailureKind ile yükselmeli; sınırlar uygulanmalı.
/// </summary>
public sealed class DotNetXsltEngineTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    private readonly DotNetXsltEngine _engine = new();

    public static TheoryData<string, string> GridCases => new()
    {
        { "invoice-content-blocks", "invoice-basic" },
        { "invoice-content-blocks", "invoice-multivalue" },
        { "invoice-ready-blocks", "invoice-rounding" },
        { "invoice-ready-blocks", "invoice-empty-numeric" },
        { "layout-scenarios", "invoice-basic" },
        { "despatch-ready-blocks", "despatch-basic" },
    };

    public static TheoryData<string, string> HandWrittenCases => new()
    {
        // method=xml + bildirim: encoding çıktı yazıcısından gelir (StringWriter → utf-16).
        {
            $$"""
            <xsl:stylesheet version="1.0" xmlns:xsl="{{XslNs}}">
              <xsl:output method="xml" indent="yes"/>
              <xsl:template match="/"><out n="{count(//i)}"><xsl:copy-of select="//i"/></out></xsl:template>
            </xsl:stylesheet>
            """,
            "<r>\n  <i>1</i>\n  <i>ş&amp;ğ</i>\n</r>"
        },
        // Boşluk düğümleri: XmlDocument varsayılanı (PreserveWhitespace=false) korunmalı.
        {
            $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
              <xsl:output method="text"/>
              <xsl:template match="/"><xsl:value-of select="count(/r/node())"/>|<xsl:value-of select="/r"/></xsl:template>
            </xsl:stylesheet>
            """,
            "<r>\n  <a>x</a>\n  <b> y </b>\n</r>"
        },
        // HTML çıktısı.
        {
            $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
              <xsl:output method="html" encoding="UTF-8"/>
              <xsl:template match="/"><html><head><title>t</title></head><body><br/><p><xsl:value-of select="/r"/></p></body></html></xsl:template>
            </xsl:stylesheet>
            """,
            "<r>a &lt; b</r>"
        },
    };

    [Theory]
    [MemberData(nameof(GridCases))]
    public void DotNetEngine_Output_BitIdenticalToXsltHtmlRenderer(string fixture, string xmlName)
    {
        var xslt = GenerateGrid(fixture);
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Xml", xmlName + ".xml"));

        AssertBitIdentical(xslt, xml);
    }

    [Theory]
    [MemberData(nameof(HandWrittenCases))]
    public void DotNetEngine_Output_BitIdenticalToXsltHtmlRenderer_HandWritten(string xslt, string xml) =>
        AssertBitIdentical(xslt, xml);

    [Fact]
    public void Kind_IsDotNet()
    {
        Assert.Equal(XsltEngineKind.DotNet, _engine.Kind);
        Assert.Equal(XsltEngineKind.DotNet, _engine.Compile(Literal("x"), XsltExecutionLimits.Default).Kind);
    }

    [Fact]
    public void Transform_ReturnsTimings_CompileMsZero()
    {
        var timings = _engine.Compile(Literal("x"), XsltExecutionLimits.Default)
            .Transform("<r/>", new StringWriter(), XsltExecutionLimits.Default);

        Assert.Equal(0, timings.CompileMs);
        Assert.Equal(timings.ParseMs + timings.TransformMs + timings.SerializeMs, timings.TotalMs);
    }

    [Fact]
    public void OutputLimit_Exceeded_Throws_OutputLimit()
    {
        var limits = XsltExecutionLimits.Default with { MaxOutputChars = 100 };
        var compiled = _engine.Compile(Literal(new string('x', 1000)), limits);
        var output = new StringWriter();

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform("<r/>", output, limits));

        Assert.Equal(XsltFailureKind.OutputLimit, ex.Diagnostic.Kind);
        Assert.Equal(XsltEngineKind.DotNet, ex.Diagnostic.Engine);
        Assert.True(output.ToString().Length <= 100);
    }

    [Fact]
    public void OutputLimit_ExactlyAtLimit_Succeeds()
    {
        var limits = XsltExecutionLimits.Default with { MaxOutputChars = 10 };
        var output = new StringWriter();

        _engine.Compile(Literal(new string('x', 10)), limits).Transform("<r/>", output, limits);

        Assert.Equal(new string('x', 10), output.ToString());
    }

    [Fact]
    public void CompileError_Throws_Compile_WithLegacyDiagnostic()
    {
        var xslt = $"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><xsl:value-of select="1 +"/></xsl:template></xsl:stylesheet>""";
        var expected = XsltDiagnostics.FromXsltException(
            Assert.ThrowsAny<System.Xml.Xsl.XsltException>(() => SecureXslt.Compile(xslt)));

        var ex = Assert.Throws<XsltProcessingException>(() => _engine.Compile(xslt, XsltExecutionLimits.Default));

        Assert.Equal(XsltFailureKind.Compile, ex.Diagnostic.Kind);
        Assert.Equal(XsltEngineKind.DotNet, ex.Diagnostic.Engine);
        Assert.Equal((expected.Message, expected.Line, expected.Column), (ex.Diagnostic.Message, ex.Diagnostic.Line, ex.Diagnostic.Column));
    }

    [Fact]
    public void RuntimeError_Throws_Runtime()
    {
        var compiled = _engine.Compile(
            $"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><xsl:message terminate="yes">dur</xsl:message></xsl:template></xsl:stylesheet>""",
            XsltExecutionLimits.Default);

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform("<r/>", new StringWriter(), XsltExecutionLimits.Default));

        Assert.Equal(XsltFailureKind.Runtime, ex.Diagnostic.Kind);
        Assert.Contains("dur", ex.Diagnostic.Message);
    }

    [Fact]
    public void MalformedXml_Throws_Xml_WithSameMessageAsRenderer()
    {
        const string xml = "<a>\n  <b></a>";
        var legacy = Assert.Throws<XmlException>(() => new XsltHtmlRenderer().Render(Literal("x"), xml));
        var compiled = _engine.Compile(Literal("x"), XsltExecutionLimits.Default);

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform(xml, new StringWriter(), XsltExecutionLimits.Default));

        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
        Assert.Equal((legacy.Message, legacy.LineNumber, legacy.LinePosition), (ex.Diagnostic.Message, ex.Diagnostic.Line, ex.Diagnostic.Column));
    }

    [Fact]
    public void XmlWithDtd_Throws_Xml()
    {
        var compiled = _engine.Compile(Literal("x"), XsltExecutionLimits.Default);
        const string xxe = """<?xml version="1.0"?><!DOCTYPE r [<!ENTITY e SYSTEM "file:///c:/windows/win.ini">]><r>&e;</r>""";

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform(xxe, new StringWriter(), XsltExecutionLimits.Default));

        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
    }

    [Fact]
    public void InputTooDeep_Throws_Xml()
    {
        var limits = XsltExecutionLimits.Default with { MaxInputDepth = 5 };
        var compiled = _engine.Compile(Literal("x"), limits);
        var deep = string.Concat(Enumerable.Repeat("<a>", 6)) + string.Concat(Enumerable.Repeat("</a>", 6));

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform(deep, new StringWriter(), limits));

        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
        Assert.True(ex.Diagnostic.Line > 0);
    }

    [Fact]
    public void InputTooManyNodes_Throws_Xml()
    {
        var limits = XsltExecutionLimits.Default with { MaxInputNodes = 50 };
        var compiled = _engine.Compile(Literal("x"), limits);
        var wide = "<r>" + string.Concat(Enumerable.Repeat("<i/>", 100)) + "</r>";

        var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform(wide, new StringWriter(), limits));

        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
    }

    [Fact]
    public void StylesheetTooDeep_Throws_LimitExceeded()
    {
        var limits = XsltExecutionLimits.Default with { MaxStylesheetDepth = 10 };
        var body = string.Concat(Enumerable.Repeat("<d>", 20)) + string.Concat(Enumerable.Repeat("</d>", 20));
        var xslt = $"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"><xsl:template match="/">{body}</xsl:template></xsl:stylesheet>""";

        var ex = Assert.Throws<XsltProcessingException>(() => _engine.Compile(xslt, limits));

        Assert.Equal(XsltFailureKind.LimitExceeded, ex.Diagnostic.Kind);
    }

    [Fact]
    public void LimitedTextWriter_PastDeadline_Throws_Timeout()
    {
        using var writer = new LimitedTextWriter(new StringWriter(), 1000, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(30);

        var ex = Assert.Throws<XsltProcessingException>(() => writer.Write("x"));

        Assert.Equal(XsltFailureKind.Timeout, ex.Diagnostic.Kind);
    }

    [Fact]
    public void LimitedTextWriter_Cancelled_Throws_Timeout_WithCode()
    {
        using var cts = new CancellationTokenSource();
        var inner = new StringWriter();
        using var writer = new LimitedTextWriter(inner, 1000, Timeout.InfiniteTimeSpan, XsltEngineKind.DotNet, cts.Token);
        writer.Write("a");
        cts.Cancel();

        var ex = Assert.Throws<XsltProcessingException>(() => writer.Write("b"));

        Assert.Equal(XsltFailureKind.Timeout, ex.Diagnostic.Kind);
        Assert.Equal(LimitedTextWriter.CancelledCode, ex.Diagnostic.ErrorCode);
        Assert.Equal("a", inner.ToString());
    }

    [Fact]
    public void Transform_CancelledLimits_StopsAtFirstWrite()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var xslt = $"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><p>x</p></xsl:template></xsl:stylesheet>""";
        var compiled = _engine.Compile(xslt, XsltExecutionLimits.Default);

        var ex = Assert.Throws<XsltProcessingException>(() =>
            compiled.Transform("<r/>", new StringWriter(), XsltExecutionLimits.Default with { Cancellation = cts.Token }));

        Assert.Equal(XsltFailureKind.Timeout, ex.Diagnostic.Kind);
        Assert.Equal(LimitedTextWriter.CancelledCode, ex.Diagnostic.ErrorCode);
    }

    [Fact]
    public void LimitedTextWriter_ForwardsEncoding_AndCounts()
    {
        var inner = new StringWriter();
        using var writer = new LimitedTextWriter(inner, 1000, Timeout.InfiniteTimeSpan);

        writer.Write('a');
        writer.Write("bc");
        writer.Write("de".ToCharArray(), 0, 2);
        writer.Write("f".AsSpan());

        Assert.Same(inner.Encoding, writer.Encoding);
        Assert.Equal("abcdef", inner.ToString());
        Assert.Equal(6, writer.CharsWritten);
    }

    private void AssertBitIdentical(string xslt, string xml)
    {
        var expected = new XsltHtmlRenderer().Render(xslt, xml).Html;

        var output = new StringWriter();
        _engine.Compile(xslt, XsltExecutionLimits.Default).Transform(xml, output, XsltExecutionLimits.Default);

        Assert.Equal(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(output.ToString()));
    }

    private static string Literal(string text) =>
        $"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"><xsl:output method="text"/><xsl:template match="/">{text}</xsl:template></xsl:stylesheet>""";

    private static string GenerateGrid(string fixture)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GridV2", fixture + ".json"));
        var tree = JsonSerializer.Deserialize<BlockTreeV2Dto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var (xslt, error) = new XsltGeneratorService().GenerateV2(tree, new Dictionary<string, string>());
        Assert.Null(error);
        return xslt!;
    }
}
