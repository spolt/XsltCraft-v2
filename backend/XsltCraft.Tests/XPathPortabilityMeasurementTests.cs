using System.Diagnostics;
using System.Text;
using System.Xml.XPath;
using System.Xml.Xsl;
using Xunit.Abstractions;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Tests;

/// <summary>
/// G3 kanıtı (CI dışı, <c>Category=Measurement</c>): <see cref="XPathPortability"/>'nin KABUL ettiği en kötü
/// ifadeler grid girdi sınırındaki (200k düğüm, derinlik ≤ 64) sentetik XML'de .NET'te 8 s grid süresinin altında.
/// Şekiller: <b>düz</b> (tek ebeveyn altında ~66k kardeş — kardeş/ebeveyn taramasının en kötüsü) ve <b>derin</b>
/// (60'lık zincir + aynı düz blok — ata dize değeri / <c>.//</c> en kötüsü).
/// </summary>
[Trait("Category", "Measurement")]
public sealed class XPathPortabilityMeasurementTests(ITestOutputHelper output)
{
    private const int Siblings = 66_000;        // s + p + metin = 3 düğüm → ~198k düğüm
    private const int Chain = 60;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(8);

    private static readonly string[] WorstAccepted =
    [
        "count(//*[not(preceding-sibling::*[1]/p[1] = p[1])])",
        "count(//*[following-sibling::node()[1]])",
        "count(//*[.//p = 'x'])",
        "count(//*[contains(., 'zz')])",
        "count(//*[count(ancestor::*) > 3])",
        "count(//*[name(..) = 'x'])",
        "count(//*[../../@w = 'x'])",
        "count(//*[@v = ../@w])",
        "count(//*[. = ../@w])",
        "count(//*[*[*[p = 'x']]])",
        "count(//p | //s | //d | //*)",
        "count(//s[p = 'x'][not(..)][count(ancestor::*) > 1][.//p])",
    ];

    [Fact]
    public void WorstAcceptedExpressions_At200kNodes_UnderGridBudget()
    {
        var shapes = new (string Name, XPathDocument Doc)[] { ("düz", Load(Build(0))), ("derin", Load(Build(Chain))) };
        var report = new StringBuilder("| ifade | düz ms | derin ms |\n|---|---|---|\n");
        var slowest = TimeSpan.Zero;

        foreach (var expr in WorstAccepted)
        {
            var r = XPathPortability.Analyze(expr);
            Assert.True(r.IsPortable, $"{expr}: {r.Message}");

            var transform = Compile(expr);
            report.Append("| `").Append(expr).Append("` ");
            foreach (var (_, doc) in shapes)
            {
                var sw = Stopwatch.StartNew();
                using var w = new StringWriter();
                transform.Transform(doc, null, w);
                sw.Stop();
                if (sw.Elapsed > slowest) slowest = sw.Elapsed;
                report.Append("| ").Append(sw.ElapsedMilliseconds).Append(' ');
                Assert.True(sw.Elapsed < Budget, $"{expr}: {sw.ElapsedMilliseconds} ms ≥ {Budget.TotalMilliseconds} ms");
            }
            report.Append("|\n");
        }

        output.WriteLine(report.ToString());
        output.WriteLine($"En yavaş: {slowest.TotalMilliseconds:F0} ms");
    }

    /// <summary>~200k düğüm: <paramref name="chain"/> derinlikli <c>d</c> zinciri altında <see cref="Siblings"/> kardeş.</summary>
    private static string Build(int chain)
    {
        var sb = new StringBuilder("<r w='q'>");
        for (int i = 0; i < chain; i++) sb.Append("<d w='q'>");
        for (int i = 0; i < Siblings; i++) sb.Append("<s v='").Append(i).Append("'><p>").Append(i).Append("</p></s>");
        for (int i = 0; i < chain; i++) sb.Append("</d>");
        return sb.Append("</r>").ToString();
    }

    private static XPathDocument Load(string xml) => new(new StringReader(xml));

    private static XslCompiledTransform Compile(string expr)
    {
        var xsl = "<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
                  $"<xsl:template match='/'><x><xsl:value-of select=\"{expr}\"/></x></xsl:template></xsl:stylesheet>";
        return SecureXslt.Compile(xsl);
    }
}
