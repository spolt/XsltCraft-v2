using System.Text;
using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class XsltSummarizerTests
{
    private const string SmallXslt = """
        <?xml version="1.0"?>
        <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:template match="/">
            <html><body>hi</body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    [Fact]
    public void Empty_input_returns_empty()
    {
        Assert.Equal(string.Empty, XsltSummarizer.Compose(null, null));
        Assert.Equal(string.Empty, XsltSummarizer.Compose(string.Empty, null));
    }

    [Fact]
    public void Small_xslt_returns_raw()
    {
        var result = XsltSummarizer.Compose(SmallXslt, null);
        Assert.Equal(SmallXslt, result);
    }

    [Fact]
    public void Large_xslt_without_templates_returns_raw()
    {
        // 8000 char dolgu — eşiği aşar, ama template yok
        var blob = "<!-- " + new string('x', 8_000) + " -->";
        var result = XsltSummarizer.Compose(blob, null);
        Assert.Equal(blob, result);
    }

    [Fact]
    public void Large_xslt_emits_template_index()
    {
        var xslt = BuildLargeXslt(templateCount: 12, bodyCharsEach: 800);
        Assert.True(xslt.Length > XsltSummarizer.RawThresholdChars);

        var result = XsltSummarizer.Compose(xslt, selection: null);

        Assert.Contains("<!-- TEMPLATE INDEX -->", result);
        Assert.Contains("<!-- L", result);
        // 12 imza listelenmiş olmalı
        var sigCount = CountOccurrences(result, "<!-- L");
        Assert.True(sigCount >= 12, $"İmza sayısı en az 12 beklenir, bulunan: {sigCount}");
        // Header korunmuş: stylesheet açılış etiketi
        Assert.Contains("<xsl:stylesheet", result);
        Assert.Contains("</xsl:stylesheet>", result);
        // Özetleme uygulandı yorumu
        Assert.Contains("xslt_summary:", result);
    }

    [Fact]
    public void Large_xslt_with_selection_includes_focused_template()
    {
        var xslt = BuildLargeXslt(templateCount: 10, bodyCharsEach: 800);
        // 5. template'in içinde özgün bir marker var → onu seçim olarak ver
        var marker = "<!-- BODY-MARKER-5 -->";
        Assert.Contains(marker, xslt);

        var result = XsltSummarizer.Compose(xslt, selection: marker);

        Assert.Contains("FOCUSED TEMPLATE", result);
        Assert.Contains(marker, result); // odak template'in tam içeriği gelmiş
        // Diğer template'lerin gövdeleri INLINE bloğuna alınmamış olmalı
        Assert.DoesNotContain("BODY-MARKER-9", result);
    }

    [Fact]
    public void Selection_not_found_falls_back_to_inline()
    {
        var xslt = BuildLargeXslt(templateCount: 6, bodyCharsEach: 900);

        var result = XsltSummarizer.Compose(xslt, selection: "<xsl:bilinmeyen-etiket/>");

        Assert.DoesNotContain("FOCUSED TEMPLATE", result);
        Assert.Contains("INLINE TEMPLATES", result);
    }

    [Fact]
    public void Summary_is_significantly_smaller_than_raw()
    {
        var xslt = BuildLargeXslt(templateCount: 20, bodyCharsEach: 800);
        var summary = XsltSummarizer.Compose(xslt, selection: null);

        // En az %40 küçülme bekliyoruz
        var ratio = (double)summary.Length / xslt.Length;
        Assert.True(ratio < 0.6,
            $"Özet/orijinal oranı 0.6'dan küçük olmalı. Bulunan: {ratio:F2} (özet={summary.Length}, orijinal={xslt.Length})");
    }

    [Fact]
    public void Malformed_template_unbalanced_does_not_crash()
    {
        var xslt = @"<?xml version=""1.0""?>
<xsl:stylesheet version=""2.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:template match=""/"">
    " + new string('A', 6_500) + @"
    <!-- bilerek kapatma etiketi eksik -->
";
        // Çökmemeli, makul bir çıktı dönmeli
        var result = XsltSummarizer.Compose(xslt, null);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static string BuildLargeXslt(int templateCount, int bodyCharsEach)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0""?>");
        sb.AppendLine(@"<xsl:stylesheet version=""2.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns:cbc=""urn:cbc"" xmlns:cac=""urn:cac"">");
        sb.AppendLine(@"  <xsl:param name=""logoPath"" select=""''""/>");

        for (int i = 0; i < templateCount; i++)
        {
            sb.AppendLine($@"  <xsl:template match=""tpl{i}"" mode=""m{i}"">");
            sb.AppendLine($@"    <!-- BODY-MARKER-{i} -->");
            sb.AppendLine($@"    {new string('B', bodyCharsEach)}");
            sb.AppendLine(@"  </xsl:template>");
        }
        sb.AppendLine(@"</xsl:stylesheet>");
        return sb.ToString();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
