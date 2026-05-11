using System.Text;
using System.Text.RegularExpressions;

namespace XsltCraft.Application.Ai;

/// <summary>
/// Büyük XSLT dosyalarını "yapısal özet"e indirir — model token'larını azaltır,
/// prefill süresini düşürür. Küçük dosyalar olduğu gibi döner.
/// </summary>
/// <remarks>
/// Strateji:
/// 1) Stylesheet header (xmlns deklarasyonlarına kadar) + xsl:param listesi tam alınır.
/// 2) Her xsl:template için yalnızca SIGNATURE (match/name/mode/priority) + satır no listelenir.
/// 3) Eğer kullanıcı bir seçim yaptıysa, seçimin DÜŞTÜĞÜ template tam içeriğiyle eklenir.
/// 4) Seçim yoksa, ilk birkaç template'in gövdesi token bütçesi dahilinde eklenir.
///
/// Regex tabanlıdır; invalid/mid-edit XSLT'lerde güvenli çalışır. XDocument.Parse kullanılmaz.
/// </remarks>
public static class XsltSummarizer
{
    /// <summary>Bu eşiğin altındaki XSLT'ler olduğu gibi döner.</summary>
    public const int RawThresholdChars = 6_000;

    /// <summary>Özetlenmiş çıktıda inline template gövdeleri için toplam karakter bütçesi.</summary>
    private const int InlineBodyBudgetChars = 4_000;

    private const string TemplateOpenLiteral = "<xsl:template";
    private const string TemplateCloseLiteral = "</xsl:template>";

    private static readonly Regex TemplateOpenRe = new(
        @"<xsl:template\b([^>]*)>",
        RegexOptions.Compiled);

    public static string Compose(string? xslt, string? selection)
    {
        if (string.IsNullOrEmpty(xslt)) return string.Empty;
        if (xslt.Length <= RawThresholdChars) return xslt;

        var templates = ParseTemplates(xslt);
        // Tanınabilir template yoksa özetleyemeyiz — ham metni dön (üst katman Clip'le sınırlar).
        if (templates.Count == 0) return xslt;

        var sb = new StringBuilder(capacity: 4096);

        // 1) Header: ilk template'in başlangıcına kadar olan kısım (xml decl + xsl:stylesheet + xmlns'ler + xsl:param'lar).
        var headerEnd = templates[0].StartIndex;
        sb.Append("<!-- xslt_summary: orijinal ").Append(xslt.Length)
          .Append(" karakter, ").Append(templates.Count).Append(" şablon — özetlendi -->\n");
        sb.Append(xslt, 0, headerEnd).TrimEndNewline();

        // 2) Template index — sadece imzalar.
        sb.Append("\n\n<!-- TEMPLATE INDEX -->\n");
        foreach (var t in templates)
            sb.Append("<!-- L").Append(t.Line).Append(": <xsl:template")
              .Append(t.Attributes).Append("> -->\n");

        // 3) Odak template: kullanıcı seçimi varsa onun düştüğü template'i tam yaz.
        var focused = FindFocusedTemplate(xslt, templates, selection);
        if (focused != null)
        {
            sb.Append("\n<!-- FOCUSED TEMPLATE (L").Append(focused.Line).Append(") -->\n");
            sb.Append(xslt, focused.StartIndex, focused.EndIndex - focused.StartIndex).Append('\n');
        }
        else
        {
            // 4) Seçim yoksa ilk birkaç template'in gövdesini bütçeye sığdığınca ver.
            sb.Append("\n<!-- INLINE TEMPLATES (token bütçesi içinde) -->\n");
            int budget = InlineBodyBudgetChars;
            foreach (var t in templates)
            {
                if (budget <= 0) break;
                var len = t.EndIndex - t.StartIndex;
                if (len > budget)
                {
                    sb.Append(xslt, t.StartIndex, budget).Append("\n<!-- ... kırpıldı -->\n");
                    break;
                }
                sb.Append(xslt, t.StartIndex, len).Append('\n');
                budget -= len;
            }
        }

        sb.Append("\n</xsl:stylesheet>");
        return sb.ToString();
    }

    internal sealed record TemplateRange(int StartIndex, int EndIndex, int Line, string Attributes);

    internal static List<TemplateRange> ParseTemplates(string xslt)
    {
        var result = new List<TemplateRange>();
        foreach (Match m in TemplateOpenRe.Matches(xslt))
        {
            // Self-closing template (<xsl:template ... />) yok sayılır — açılış-kapanışı yoksa indeksleyemeyiz.
            // Regex zaten "/>" sonrası yakalamadığı için match.Length sonu '>'dur.
            // Eşleşme sonrası gelen karakter '/>' olduysa atla.
            if (m.Index + m.Length - 2 >= 0 && xslt[m.Index + m.Length - 2] == '/')
                continue;

            int afterOpen = m.Index + m.Length;
            int endTagStart = FindMatchingEnd(xslt, afterOpen);
            if (endTagStart < 0) continue; // unbalanced — atla

            int endIndex = endTagStart + TemplateCloseLiteral.Length;
            int line = CountLines(xslt, m.Index);
            result.Add(new TemplateRange(m.Index, endIndex, line, m.Groups[1].Value));
        }
        return result;
    }

    /// <summary>
    /// afterOpen pozisyonundan başlayarak eşleşen "&lt;/xsl:template&gt;"in başlangıç indeksini döner.
    /// İç içe xsl:template'i (nadir) depth sayacıyla destekler.
    /// </summary>
    private static int FindMatchingEnd(string xslt, int afterOpen)
    {
        int depth = 1;
        int idx = afterOpen;
        while (idx < xslt.Length)
        {
            int nextOpen = xslt.IndexOf(TemplateOpenLiteral, idx, StringComparison.Ordinal);
            int nextClose = xslt.IndexOf(TemplateCloseLiteral, idx, StringComparison.Ordinal);
            if (nextClose < 0) return -1;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                // İç template — self-closing değilse depth artır.
                int gt = xslt.IndexOf('>', nextOpen);
                if (gt < 0) return -1;
                bool selfClosing = gt > 0 && xslt[gt - 1] == '/';
                if (!selfClosing) depth++;
                idx = gt + 1;
            }
            else
            {
                depth--;
                if (depth == 0) return nextClose;
                idx = nextClose + TemplateCloseLiteral.Length;
            }
        }
        return -1;
    }

    private static int CountLines(string s, int pos)
    {
        int count = 1;
        int len = Math.Min(pos, s.Length);
        for (int i = 0; i < len; i++)
            if (s[i] == '\n') count++;
        return count;
    }

    private static TemplateRange? FindFocusedTemplate(
        string xslt, List<TemplateRange> templates, string? selection)
    {
        if (string.IsNullOrWhiteSpace(selection)) return null;
        // Seçimin XSLT içindeki konumu — uzun seçimde ilk ~120 karakter yeterli anchor.
        var anchor = selection.Length > 120 ? selection[..120] : selection;
        int idx = xslt.IndexOf(anchor, StringComparison.Ordinal);
        if (idx < 0) return null;
        foreach (var t in templates)
            if (idx >= t.StartIndex && idx < t.EndIndex) return t;
        return null;
    }
}

internal static class StringBuilderExtensions
{
    public static StringBuilder TrimEndNewline(this StringBuilder sb)
    {
        while (sb.Length > 0 && (sb[^1] == '\n' || sb[^1] == '\r'))
            sb.Length--;
        return sb;
    }
}
