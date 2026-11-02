using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Tests.Support;

/// <summary>Ortak test yardımcıları. Motor örnekleri paylaşılır (Processor thread-safe; soğuk açılış ~0.7 sn).</summary>
internal static class SaxonTestKit
{
    public const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    private static readonly Lazy<SaxonXsltEngine> SharedIsolated = new(() => new SaxonXsltEngine(SaxonEngineOptions.Isolated));
    private static readonly Lazy<SaxonXsltEngine> SharedGrid = new(() => new SaxonXsltEngine(SaxonEngineOptions.InProcessGrid));

    public static SaxonXsltEngine Engine => SharedIsolated.Value;

    public static SaxonXsltEngine GridEngine => SharedGrid.Value;

    public static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string GibGeneral => File.ReadAllText(Path.Combine(FixturesDir, "gib-general.xslt"));

    public static string InvoiceXml => File.ReadAllText(Path.Combine(FixturesDir, "invoice.xml"));

    /// <summary>Gövdeyi <c>match="/"</c> şablonuna saran 2.0 stylesheet.</summary>
    public static string Stylesheet(string body, string top = "", string version = "2.0", string extraNs = "") => $"""
        <xsl:stylesheet version="{version}" xmlns:xsl="{XslNs}" xmlns:xs="http://www.w3.org/2001/XMLSchema" exclude-result-prefixes="xs" {extraNs}>
          <xsl:output method="xml" omit-xml-declaration="yes"/>
          {top}
          <xsl:template match="/"><out>{body}</out></xsl:template>
        </xsl:stylesheet>
        """;

    public static string Render(string xslt, string xml = "<r/>", XsltExecutionLimits? limits = null, SaxonXsltEngine? engine = null)
    {
        limits ??= XsltExecutionLimits.Default;
        var compiled = (engine ?? Engine).Compile(xslt, limits);
        var sw = new StringWriter();
        compiled.Transform(xml, sw, limits);
        return sw.ToString();
    }

    /// <summary>Derleme veya dönüşüm hatası beklenir; teşhisi döner.</summary>
    public static XsltDiagnostic Fail(string xslt, string xml = "<r/>", XsltExecutionLimits? limits = null, SaxonXsltEngine? engine = null)
    {
        var ex = Assert.Throws<XsltProcessingException>(() => Render(xslt, xml, limits, engine));
        Assert.Equal(XsltEngineKind.Saxon, ex.Diagnostic.Engine);
        return ex.Diagnostic;
    }

    /// <summary>Derin özyineleme testleri için büyük yığınlı thread (worker EngineThread'i 64 MB).</summary>
    public static T OnBigStack<T>(Func<T> body, int stackMb = 64)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = body();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        }, stackMb * 1024 * 1024);
        thread.Start();
        thread.Join();
        if (error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return result!;
    }

    public static string Deep(string name, int levels) =>
        string.Concat(Enumerable.Repeat($"<{name}>", levels)) + string.Concat(Enumerable.Repeat($"</{name}>", levels));
}
