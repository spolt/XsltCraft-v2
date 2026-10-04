using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class DetectXsltVersionTests
{
    private const string Ns = "xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"";

    [Theory]
    // Regresyon: XML bildirimi ilk "version=" idi → 2.0 şablonu "1.0" okunuyordu.
    [InlineData($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<xsl:stylesheet version=\"2.0\" {Ns}/>", "2.0")]
    [InlineData($"<?xml version=\"1.0\"?><xsl:stylesheet {Ns} version=\"3.0\"/>", "3.0")]
    [InlineData($"<xsl:stylesheet version='1.0' {Ns}/>", "1.0")]
    [InlineData($"<xsl:transform version=\"2.0\" {Ns}/>", "2.0")]
    // Öznitelikler çok satıra yayılmış (gerçek GİB şablonları) + boşluklu "="
    [InlineData($"<?xml version=\"1.0\"?>\n<xsl:stylesheet\n  {Ns}\n  xmlns:cbc=\"urn:x\"\n  version = \"2.0\">", "2.0")]
    // Farklı önek
    [InlineData("<x:stylesheet version=\"2.0\" xmlns:x=\"http://www.w3.org/1999/XSL/Transform\"/>", "2.0")]
    // Stylesheet içindeki başka version= değerleri (xsl:output) etkilemez
    [InlineData($"<?xml version=\"1.0\"?><xsl:stylesheet version=\"2.0\" {Ns}><xsl:output version=\"4.0\" method=\"html\"/></xsl:stylesheet>", "2.0")]
    public void ReadsRootStylesheetVersion(string xslt, string expected)
        => Assert.Equal(expected, PromptTemplates.DetectXsltVersion(xslt));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<?xml version=\"1.0\"?><root/>")]   // stylesheet yok → varsayılan
    [InlineData("<xsl:stylesheet")]                  // yarım yazılmış (editör ortası)
    public void FallsBackTo20(string? xslt)
        => Assert.Equal("2.0", PromptTemplates.DetectXsltVersion(xslt));
}
