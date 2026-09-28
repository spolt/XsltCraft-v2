using System.Text;
using System.Xml;
using System.Xml.Xsl;

using XsltCraft.Application.Xslt;

namespace XsltCraft.Tests;

/// <summary>
/// SecureXslt.Compile — stylesheet derlenirken dış kaynak çözümlenmez (yerel dosya okuma / SSRF).
/// </summary>
public sealed class SecureXsltTests : IDisposable
{
    private const string Marker = "GIZLI_DOSYA_ICERIGI";
    private readonly string _dir = Directory.CreateTempSubdirectory("securexslt").FullName;
    private readonly string _secretPath;

    public SecureXsltTests()
    {
        // Sunucudaki "hassas" dosyayı temsil eden, include edilebilir bir stylesheet.
        _secretPath = Path.Combine(_dir, "secret.xsl");
        File.WriteAllText(_secretPath, $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template name="leak"><xsl:text>{Marker}</xsl:text></xsl:template>
            </xsl:stylesheet>
            """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Including(string href) => $"""
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:include href="{href}"/>
          <xsl:template match="/"><out><xsl:call-template name="leak"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    private static string Run(XslCompiledTransform transform)
    {
        var sb = new StringBuilder();
        using var input = XmlReader.Create(new StringReader("<r/>"));
        using var writer = new StringWriter(sb);
        transform.Transform(input, null, writer);
        return sb.ToString();
    }

    [Fact]
    public void XmlUrlResolver_WouldReadLocalFile()
    {
        // Düzeltme öncesi davranışın kanıtı: resolver verilirse yerel dosya stylesheet'e girer.
        var transform = new XslCompiledTransform();
        using var reader = XmlReader.Create(new StringReader(Including(new Uri(_secretPath).AbsoluteUri)), SecureXslt.ReaderSettings());
        transform.Load(reader, SecureXslt.Settings, new XmlUrlResolver());

        Assert.Contains(Marker, Run(transform));
    }

    [Fact]
    public void Compile_RejectsAbsoluteFileInclude()
    {
        Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(Including(new Uri(_secretPath).AbsoluteUri)));
    }

    [Fact]
    public void Compile_RejectsRelativeInclude()
    {
        // Base URI yokken göreli href çalışma dizinine göre çözülürdü.
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), _secretPath).Replace('\\', '/');
        Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(Including(relative)));
    }

    [Fact]
    public void Compile_RejectsHttpImport()
    {
        const string xslt = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:import href="http://169.254.169.254/latest/meta-data/"/>
            </xsl:stylesheet>
            """;
        Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(xslt));
    }

    [Fact]
    public void Compile_RejectsDtd()
    {
        const string xslt = """
            <!DOCTYPE x [ <!ENTITY e SYSTEM "file:///etc/passwd"> ]>
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"/>
            """;
        var ex = Assert.ThrowsAny<XsltException>(() => SecureXslt.Compile(xslt));
        Assert.IsType<XmlException>(ex.InnerException);
    }

    [Fact]
    public void Compile_DocumentFunctionIsDisabled()
    {
        var xslt = $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/"><xsl:copy-of select="document('{new Uri(_secretPath).AbsoluteUri}')"/></xsl:template>
            </xsl:stylesheet>
            """;
        var transform = SecureXslt.Compile(xslt);
        Assert.ThrowsAny<XsltException>(() => Run(transform));
    }

    [Fact]
    public void Compile_SelfContainedStylesheet_Works()
    {
        const string xslt = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/"><ok/></xsl:template>
            </xsl:stylesheet>
            """;
        Assert.Contains("<ok", Run(SecureXslt.Compile(xslt)));
    }
}
