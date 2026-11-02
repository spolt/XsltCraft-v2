using System.Xml;
using System.Xml.Xsl;

using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Tests.Xslt;

/// <summary>
/// PreviewController.DescribeXsltError mantığı XsltDiagnostics'e taşındı; denetleyici artık yalnız
/// yönlendirir. Bu proje Api'ye referans vermediği için eski davranış, taşımadan önceki gövdenin
/// birebir kopyası olan <see cref="LegacyDescribeXsltError"/> kahini ile karşılaştırılır.
/// </summary>
public sealed class XsltDiagnosticContractTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    // Taşımadan önceki PreviewController.DescribeXsltError (d1c086d) — DEĞİŞTİRME.
    private static (string Message, int Line, int Column) LegacyDescribeXsltError(XsltException ex)
    {
        int line = ex.LineNumber;
        int column = ex.LinePosition;

        var messages = new List<string>();
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message))
                messages.Add(cur.Message.Trim());
            if ((line == 0 || column == 0) && cur is XsltException xe && xe.LineNumber != 0)
            {
                line = xe.LineNumber;
                column = xe.LinePosition;
            }
        }

        var message = string.Join(" → ", messages.Distinct());
        return (message, line, column);
    }

    private static readonly string InvalidXPathStylesheet = $"""
        <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
          <xsl:template match="/"><out><xsl:value-of select="1 +"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    public static TheoryData<string> CompileErrorStylesheets => new()
    {
        // Geçersiz XPath — asıl neden InnerException'da.
        InvalidXPathStylesheet,
        // Bilinmeyen xsl elemanı.
        $"""
        <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
          <xsl:template match="/"><xsl:bogus/></xsl:template>
        </xsl:stylesheet>
        """,
        // Yanlış konumlanmış eleman.
        $"""
        <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
          <xsl:value-of select="'x'"/>
        </xsl:stylesheet>
        """,
    };

    [Theory]
    [MemberData(nameof(CompileErrorStylesheets))]
    public void DescribeXsltError_Forwarder_SameOutput_CompileError(string xslt)
    {
        var ex = Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(xslt));

        AssertSame(LegacyDescribeXsltError(ex), XsltDiagnostics.FromXsltException(ex));
    }

    [Fact]
    public void DescribeXsltError_Forwarder_SameOutput_RuntimeError()
    {
        var transform = SecureXslt.Compile($"""
            <xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}">
              <xsl:template match="/">
                <out><xsl:message terminate="yes">dur</xsl:message></out>
              </xsl:template>
            </xsl:stylesheet>
            """);
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml("<r/>");

        var ex = Assert.ThrowsAny<XsltException>(() => transform.Transform(doc, null, new StringWriter()));

        var diag = XsltDiagnostics.FromXsltException(ex, XsltFailureKind.Runtime, XsltEngineKind.DotNet);
        AssertSame(LegacyDescribeXsltError(ex), diag);
        Assert.Equal(XsltFailureKind.Runtime, diag.Kind);
        Assert.Equal(XsltEngineKind.DotNet, diag.Engine);
    }

    [Fact]
    public void DescribeXsltError_Forwarder_SameOutput_XmlException()
    {
        var ex = Assert.Throws<XmlException>(() =>
            new XsltHtmlRenderer().Render($"""<xsl:stylesheet version="1.0" xmlns:xsl="{XslNs}"/>""", "<a>\n  <b></a>"));

        var diag = XsltDiagnostics.FromXmlException(ex);

        // Denetleyicinin XmlException yanıtı: { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition }.
        AssertSame((ex.Message, ex.LineNumber, ex.LinePosition), diag);
        Assert.Equal(XsltFailureKind.Xml, diag.Kind);
        Assert.Null(diag.Engine);
    }

    [Fact]
    public void FromXsltException_Defaults_CompileKind_NoEngine()
    {
        var ex = Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(InvalidXPathStylesheet));

        var diag = XsltDiagnostics.FromXsltException(ex);

        Assert.Equal(XsltFailureKind.Compile, diag.Kind);
        Assert.Null(diag.Engine);
        Assert.Null(diag.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(diag.Message));
        Assert.True(diag.Line > 0);
    }

    [Fact]
    public void XsltProcessingException_CarriesDiagnostic()
    {
        var diag = new XsltDiagnostic("m", 3, 4, XsltFailureKind.OutputLimit, XsltEngineKind.DotNet, "E1");
        var inner = new InvalidOperationException();

        var ex = new XsltProcessingException(diag, inner);

        Assert.Same(diag, ex.Diagnostic);
        Assert.Equal("m", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    private static void AssertSame((string Message, int Line, int Column) legacy, XsltDiagnostic diag)
    {
        Assert.Equal(legacy.Message, diag.Message);
        Assert.Equal(legacy.Line, diag.Line);
        Assert.Equal(legacy.Column, diag.Column);
    }
}
