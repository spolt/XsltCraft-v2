using XsltCraft.Application.Xslt.Engines;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// SR3: worker'da stdout/stderr protokol kanalıdır; Api'de log. xsl:message, trace(), derleme uyarıları ve
/// hatalar hiçbir standart akışa yazılmamalı. Global akışları değiştirdiği için paralel çalışmaz.
/// </summary>
[Collection(nameof(StdioCollection))]
public sealed class SaxonStdioTests
{
    [Fact]
    public void XslMessage_And_Trace_ProduceNoStderr()
    {
        var javaErr = new java.io.ByteArrayOutputStream();
        var javaOut = new java.io.ByteArrayOutputStream();
        var dotnetErr = new StringWriter();
        var dotnetOut = new StringWriter();
        var oldJavaErr = java.lang.System.err;
        var oldJavaOut = java.lang.System.@out;
        var oldErr = Console.Error;
        var oldOut = Console.Out;

        java.lang.System.setErr(new java.io.PrintStream(javaErr, true));
        java.lang.System.setOut(new java.io.PrintStream(javaOut, true));
        Console.SetError(dotnetErr);
        Console.SetOut(dotnetOut);
        try
        {
            // Mesaj + trace() + derleme uyarısı (SXWN9001) + çalışma hatası + derleme hatası.
            var ok = Stylesheet("<a><xsl:variable name=\"v\" select=\"1\"/></a><xsl:message>MESAJ_STDERR</xsl:message><xsl:value-of select=\"trace('TRACE_STDERR', 'etiket')\"/>");
            Assert.Contains("TRACE_STDERR", Render(ok));
            Assert.Throws<XsltProcessingException>(() => Render(Stylesheet("<xsl:message terminate=\"yes\">SONLANDIR_STDERR</xsl:message>")));
            Assert.Throws<XsltProcessingException>(() => Render(Stylesheet("<xsl:value-of select=\"$yok\"/>")));
            Assert.Throws<XsltProcessingException>(() => Render(Stylesheet("<xsl:copy-of select=\"doc('file:///etc/passwd')\"/>")));
        }
        finally
        {
            java.lang.System.err.flush();
            java.lang.System.@out.flush();
            java.lang.System.setErr(oldJavaErr);
            java.lang.System.setOut(oldJavaOut);
            Console.SetError(oldErr);
            Console.SetOut(oldOut);
        }

        Assert.Equal(string.Empty, javaErr.toString());
        Assert.Equal(string.Empty, javaOut.toString());
        Assert.Equal(string.Empty, dotnetErr.ToString());
        Assert.Equal(string.Empty, dotnetOut.ToString());
    }
}

[CollectionDefinition(nameof(StdioCollection), DisableParallelization = true)]
public sealed class StdioCollection
{
}
