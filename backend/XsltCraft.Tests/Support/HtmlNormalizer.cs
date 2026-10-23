using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace XsltCraft.Tests.Support;

/// <summary>
/// Golden HTML snapshot'ları için motor-bağımsız kanonik serileştirici.
///
/// Amaç: aynı DOM'u üreten iki XSLT motorunun (.NET XslCompiledTransform / Saxon HE)
/// serileştirme farklarını (DOCTYPE, otomatik meta, girinti, entity yazımı, öznitelik sırası)
/// görünmez kılıp yalnız anlamsal farkları snapshot'ta bırakmak.
///
/// Kurallar:
/// <list type="bullet">
/// <item>HTML5 ayrıştırıcıyla (AngleSharp) DOM'a parse edilir; serileştirme <c>&lt;html&gt;</c> kökünden başlar → DOCTYPE (varsa, hangi biçimde olursa olsun) atılır.</item>
/// <item><c>&lt;meta http-equiv&gt;</c> atılır (motorlar html çıktı yöntemiyle kendiliğinden ekler); <c>&lt;meta charset&gt;</c> korunur.</item>
/// <item>Yorumlar atılır.</item>
/// <item>Metin düğümlerinde HTML boşluk karakterleri (space/tab/CR/LF/FF) tek boşluğa indirgenir ve kırpılır; yalnız boşluktan oluşan (girinti) düğümler atılır. NBSP (U+00A0) boşluk sayılmaz.</item>
/// <item>Entity'ler çözülüp tek biçimde yazılır: metinde yalnız <c>&amp;amp; &amp;lt; &amp;gt;</c>, öznitelikte <c>&amp;amp; &amp;quot;</c>; NBSP her yerde görünür olsun diye <c>&amp;nbsp;</c>.</item>
/// <item>Öznitelikler ada göre (ordinal) sıralanır; değerlerdeki boşluk dizileri indirgenir.</item>
/// <item><c>script</c>/<c>style</c> içeriği ham kalır, yalnız boşlukları indirgenir.</item>
/// <item>Her eleman kendi satırında, 2 boşluk girintiyle; tek metin çocuğu olan eleman tek satırda yazılır.</item>
/// </list>
/// </summary>
public static partial class HtmlNormalizer
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style",
    };

    public static string Normalize(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var sb = new StringBuilder();
        if (document.DocumentElement is { } root)
            WriteElement(sb, root, depth: 0);
        return sb.ToString();
    }

    private static void WriteElement(StringBuilder sb, IElement element, int depth)
    {
        var name = element.LocalName;
        Indent(sb, depth);
        sb.Append('<').Append(name);
        foreach (var attr in element.Attributes.OrderBy(a => a.Name, StringComparer.Ordinal))
            sb.Append(' ').Append(attr.Name).Append("=\"").Append(EscapeAttribute(CollapseWhitespace(attr.Value))).Append('"');
        sb.Append('>');

        if (VoidElements.Contains(name))
        {
            sb.Append('\n');
            return;
        }

        if (RawTextElements.Contains(name))
        {
            sb.Append(CollapseWhitespace(element.TextContent)).Append("</").Append(name).Append(">\n");
            return;
        }

        var children = SignificantChildren(element).ToList();
        if (children.Count == 0)
        {
            sb.Append("</").Append(name).Append(">\n");
            return;
        }

        if (children is [IText onlyText])
        {
            sb.Append(EscapeText(CollapseWhitespace(onlyText.Data))).Append("</").Append(name).Append(">\n");
            return;
        }

        sb.Append('\n');
        foreach (var child in children)
        {
            if (child is IElement childElement)
            {
                WriteElement(sb, childElement, depth + 1);
            }
            else if (child is IText text)
            {
                Indent(sb, depth + 1);
                sb.Append(EscapeText(CollapseWhitespace(text.Data))).Append('\n');
            }
        }
        Indent(sb, depth);
        sb.Append("</").Append(name).Append(">\n");
    }

    private static IEnumerable<INode> SignificantChildren(IElement element)
    {
        foreach (var node in element.ChildNodes)
        {
            switch (node)
            {
                case IElement child when IsHttpEquivMeta(child):
                    continue;
                case IElement child:
                    yield return child;
                    break;
                case IText text when CollapseWhitespace(text.Data).Length > 0:
                    yield return text;
                    break;
                // Yorum, işleme talimatı ve yalnız-boşluk metin düğümleri atılır.
            }
        }
    }

    private static bool IsHttpEquivMeta(IElement element) =>
        string.Equals(element.LocalName, "meta", StringComparison.OrdinalIgnoreCase)
        && element.HasAttribute("http-equiv");

    /// <summary>Yalnız HTML boşluk karakterleri (NBSP hariç) tek boşluğa indirgenir.</summary>
    private static string CollapseWhitespace(string value) =>
        HtmlWhitespace().Replace(value, " ").Trim(' ');

    private static string EscapeText(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace(" ", "&nbsp;");

    private static string EscapeAttribute(string value) => value
        .Replace("&", "&amp;")
        .Replace("\"", "&quot;")
        .Replace(" ", "&nbsp;");

    private static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

    [GeneratedRegex("[ \t\r\n\f]+")]
    private static partial Regex HtmlWhitespace();
}
