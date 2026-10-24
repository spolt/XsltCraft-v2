using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Diagnostics;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Sandbox;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>SR10 / G0 Y-3: hata eşleme ve mesaj temizliği.</summary>
public sealed class SaxonDiagnosticsTests
{
    [Fact]
    public void CompileErrorList_LineColumn()
    {
        var xslt = $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}">
              <xsl:template match="/">
                <out>
                  <xsl:value-of select="bilinmeyen-fonksiyon(1)"/>
                </out>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(xslt, XsltExecutionLimits.Default)).Diagnostic;

        Assert.Equal(XsltFailureKind.Compile, d.Kind);
        Assert.Equal("XPST0017", d.ErrorCode);
        Assert.Equal(4, d.Line);
        Assert.Contains("bilinmeyen-fonksiyon", d.Message);

        // Saxon XPath ifade hatalarında kolon vermez (0); eleman/öznitelik hatalarında verir.
        var attrError = $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}">
              <xsl:template match="/">
                <xsl:value-of select="1" foo="x"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var a = Assert.Throws<XsltProcessingException>(() => Engine.Compile(attrError, XsltExecutionLimits.Default)).Diagnostic;
        Assert.Equal("XTSE0090", a.ErrorCode);
        Assert.Equal(3, a.Line);
        Assert.True(a.Column > 0, $"kolon {a.Column}");
    }

    [Fact]
    public void InlineFunctionSyntax_RejectedAtCompile()
    {
        // XPATH_VERSION_FOR_XSLT=20: satır içi function() 2.0 ayrıştırıcısında anlamsız → derleme hatası.
        var d = Assert.Throws<XsltProcessingException>(() =>
            Engine.Compile(Stylesheet("<xsl:value-of select=\"function($x){$x}(1)\"/>"), XsltExecutionLimits.Default)).Diagnostic;
        Assert.True(d.Kind is XsltFailureKind.Compile or XsltFailureKind.Unsupported, d.Kind.ToString());
    }

    [Fact]
    public void Warnings_Filtered()
    {
        // Etkisiz değişken SXWN9001 uyarısı üretir (önce gelir); ardından gerçek hata.
        // Uyarı teşhise sızmamalı, ilk HATA esas alınmalı.
        var xslt = Stylesheet("<a><xsl:variable name=\"v\" select=\"1\"/></a><xsl:value-of select=\"$tanimsiz\"/>");
        var factory = new SaxonProcessorFactory(SaxonEngineOptions.Isolated);
        var processor = factory.CreateProcessor();
        var reporter = new BoundedErrorReporter(4, 512);
        var compiler = factory.CreateCompiler(processor, reporter);

        Assert.ThrowsAny<Exception>(() => compiler.compile(Input.StylesheetSource.Create(xslt)));

        Assert.True(reporter.WarningCount > 0, "beklenen derleme uyarısı üretilmedi");
        Assert.NotEmpty(reporter.Errors);
        Assert.All(reporter.Errors, e => Assert.False(e.IsWarning));

        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(xslt, XsltExecutionLimits.Default)).Diagnostic;
        Assert.Equal("XPST0008", d.ErrorCode);
    }

    [Fact]
    public void ErrorReporter_IsBounded()
    {
        var body = string.Concat(Enumerable.Range(0, 50).Select(i => $"<xsl:value-of select=\"$yok{i}\"/>"));
        var factory = new SaxonProcessorFactory(SaxonEngineOptions.Isolated);
        var reporter = new BoundedErrorReporter(3, 64);
        var compiler = factory.CreateCompiler(factory.CreateProcessor(), reporter);

        Assert.ThrowsAny<Exception>(() => compiler.compile(Input.StylesheetSource.Create(Stylesheet(body))));

        Assert.Equal(3, reporter.Errors.Count);
        Assert.True(reporter.DroppedErrorCount > 0);
        Assert.All(reporter.Errors, e => Assert.True(e.Message.Length <= 65));
    }

    [Fact]
    public void SandboxViolation_InCauseChain_Security()
    {
        // Gerçek motor: doc() → reddeden ResourceResolver → Saxon sarar → cause zinciri.
        var d = Fail(Stylesheet("<xsl:copy-of select=\"doc('file:///etc/passwd')\"/>"));
        Assert.Equal(XsltFailureKind.Security, d.Kind);
        Assert.Equal(SandboxCodes.ResourceDenied, d.ErrorCode);

        // Birim: iki kat sarılmış ihlal.
        var violation = new SandboxViolationException(SandboxCodes.CollectionDenied, "collection() yasak.");
        var wrapped = new net.sf.saxon.trans.XPathException("dış hata", "FODC0002");
        wrapped.initCause(violation);
        var outer = new net.sf.saxon.s9api.SaxonApiException(wrapped);

        var mapped = SaxonDiagnostics.FromRuntime(outer, tripped: null);
        Assert.Equal(XsltFailureKind.Security, mapped.Kind);
        Assert.Equal(SandboxCodes.CollectionDenied, mapped.ErrorCode);
        Assert.Equal("collection() yasak.", mapped.Message);
    }

    [Fact]
    public void SandboxViolation_MessageOnly_StillSecurity()
    {
        // Saxon cause'u düşürüp yalnız mesajı kopyalarsa önek üzerinden tanınır.
        var copied = new net.sf.saxon.trans.XPathException(
            SandboxViolationException.Marker + SandboxCodes.UnparsedTextDenied + "] Metin okuma yasak. file:///C:/gizli.txt");
        var mapped = SaxonDiagnostics.FromRuntime(copied, tripped: null);

        Assert.Equal(XsltFailureKind.Security, mapped.Kind);
        Assert.Equal(SandboxCodes.UnparsedTextDenied, mapped.ErrorCode);
        Assert.DoesNotContain("gizli", mapped.Message);
    }

    [Fact]
    public void Throwable_MapsToEngineInternal()
    {
        foreach (var ex in new Exception[]
        {
            new java.lang.NoSuchMethodError("java.nio.ByteBuffer.clear()Ljava/nio/ByteBuffer;"),
            new java.lang.IllegalStateException("net.sf.saxon.tree.tiny.TinyTree bozuk"),
            new InvalidOperationException("cli.System.Foo"),
        })
        {
            var d = SaxonDiagnostics.FromRuntime(ex, tripped: null);
            Assert.Equal(XsltFailureKind.EngineInternal, d.Kind);
            Assert.DoesNotContain("java", d.Message);
            Assert.DoesNotContain("ByteBuffer", d.Message);

            var c = SaxonDiagnostics.FromCompile(ex, []);
            Assert.Equal(XsltFailureKind.EngineInternal, c.Kind);
        }
    }

    [Fact]
    public void TrippedBudget_WinsOverWrappedException()
    {
        var limit = new SaxonLimitError(XsltFailureKind.OutputLimit, LimitCodes.Output, "Çıktı sınırı.");
        var wrapped = new net.sf.saxon.s9api.SaxonApiException(new net.sf.saxon.trans.XPathException("I/O error"));

        var d = SaxonDiagnostics.FromRuntime(wrapped, limit);

        Assert.Equal(XsltFailureKind.OutputLimit, d.Kind);
        Assert.Equal(LimitCodes.Output, d.ErrorCode);
    }

    [Fact]
    public void Messages_NoJavaClassNamesOrPaths()
    {
        var secretDir = Directory.CreateTempSubdirectory("xc-diag").FullName;
        try
        {
            var path = Path.Combine(secretDir, "yok.xml");
            var uri = new Uri(path).AbsoluteUri;
            var cases = new[]
            {
                Stylesheet($"<xsl:copy-of select=\"doc('{uri}')\"/>"),
                Stylesheet($"<xsl:value-of select=\"unparsed-text('{uri}')\"/>"),
                Stylesheet("<xsl:value-of select=\"xs:integer('abc')\"/>"),
                Stylesheet("<xsl:value-of select=\"1 div 0\"/>"),
                Stylesheet($"<xsl:copy-of select=\"doc('{path.Replace('\\', '/')}')\"/>"),
                $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:include href=\"{uri}\"/></xsl:stylesheet>",
            };

            foreach (var xslt in cases)
            {
                var d = Fail(xslt);
                Assert.False(SaxonMessageSanitizer.LooksLeaky(d.Message), d.Message);
                Assert.DoesNotContain(secretDir, d.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(secretDir.Replace('\\', '/'), d.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("org.xml.sax", d.Message);
                Assert.DoesNotContain("cli.", d.Message);
            }
        }
        finally
        {
            Directory.Delete(secretDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("I/O error reported by XML parser processing file:/C:/Users/x/a.xml. Caused by java.io.FileNotFoundException: C:\\Users\\x\\a.xml (bulunamadı)")]
    [InlineData("net.sf.saxon.trans.XPathException: Bir hata /app/appsettings.json")]
    [InlineData("org.xml.sax.SAXParseException; lineNumber: 1; cli.System.IO.IOException at \\\\sunucu\\pay\\x")]
    [InlineData("jar:file:/opt/app/Saxon.HE.dll!/x.xml classpath:META-INF/MANIFEST.MF")]
    public void Sanitizer_RemovesInternals(string raw)
    {
        var clean = SaxonMessageSanitizer.Clean(raw);
        Assert.False(SaxonMessageSanitizer.LooksLeaky(clean), clean);
        Assert.DoesNotContain("Users", clean);
        Assert.DoesNotContain("/app/", clean);
        Assert.DoesNotContain("sunucu", clean);
    }

    [Theory]
    [InlineData("<xsl:value-of select=\"let $x := 1 return $x\"/>")]
    [InlineData("<xsl:value-of select=\"(1, 2) ! (. * 2)\"/>")]
    [InlineData("<xsl:value-of select=\"map{'a':1}?a\"/>")]
    public void XPath3Syntax_TurkishHint(string body)
    {
        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(Stylesheet(body), XsltExecutionLimits.Default)).Diagnostic;

        Assert.Equal(XsltFailureKind.Unsupported, d.Kind);
        Assert.Equal(SaxonDiagnostics.XPath3SyntaxMessage, d.Message);
        Assert.Equal("XPST0003", d.ErrorCode);
    }
}
