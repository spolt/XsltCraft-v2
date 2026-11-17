using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.Xslt.NoteLoops;

namespace XsltCraft.Application.Xslt;

/// <summary>
/// Sabit not satırını şablonun kendi not döngüsünün gövdesinden türetir (şablonun not stili korunur): değer
/// yer-tutucusu (<c>xsl:value-of select="."</c> vb.) sabit metinle değişir, yer-tutucuyu saran koşullar
/// (döngü dışında anlamsız not filtreleri) açılır; kapsayıcı satır/hücre/liste bekliyorsa uygun sarmalayıcı eklenir.
/// Metin yalnız <c>xsl:text</c> text node'udur (otomatik kaçış).
/// </summary>
internal static class FixedNoteSnippet
{
    private static readonly XNamespace Xsl = NoteLoopClassifier.Xsl;

    private static readonly HashSet<string> Placeholders = new(StringComparer.Ordinal)
    {
        ".", "string(.)", "normalize-space(.)", "normalize-space()", "text()", "current()",
    };

    public static string Build(NotesTarget target, string noteText)
    {
        var nodes = (target.StyleLoop is { } loop ? Derive(loop, noteText) : null) ?? Default(noteText);
        return Serialize(WrapForContext(nodes, target));
    }

    private static List<XNode>? Derive(XElement loop, string noteText)
    {
        var root = new XElement(Xsl + "root", loop.Nodes()
            .Where(n => n is not XComment && !(n is XText t && string.IsNullOrWhiteSpace(t.Value)))
            .Select(Clone));
        foreach (var t in root.DescendantNodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).ToList())
            t.Remove();
        foreach (var c in root.DescendantNodes().OfType<XComment>().ToList())
            c.Remove();

        var placeholder = root.Descendants(Xsl + "value-of")
            .FirstOrDefault(v => Placeholders.Contains(Collapse((string?)v.Attribute("select") ?? "")));
        if (placeholder is null)
            return null;

        var text = new XElement(Xsl + "text", noteText);
        placeholder.ReplaceWith(text);

        // Yer-tutucuyu saran koşulları içten dışa aç (düğümler önce koparılır ki kopyalanmasın, referans korunsun).
        while (text.Ancestors().FirstOrDefault(a => !ReferenceEquals(a, root) && a.Name.Namespace == Xsl
                   && a.Name.LocalName is "if" or "when" or "otherwise") is { } cond)
        {
            var target = cond.Name.LocalName == "if" ? cond : cond.Parent!;
            var kids = cond.Nodes().ToList();
            foreach (var k in kids)
                k.Remove();
            if (ReferenceEquals(target, root))
                break;
            target.ReplaceWith(kids);
        }

        // Döngü dışında bağlam (.) artık not değil: gövdede başka XSL komutu kalırsa (ör. ikinci bir if,
        // position(), iç for-each, xsl:attribute) şablon stili güvenli taşınamaz → bağlama duyarlı varsayılan.
        if (root.Descendants().Any(d => d.Name.Namespace == Xsl && d.Name != Xsl + "text")
            || text.Ancestors().TakeWhile(a => !ReferenceEquals(a, root)).Any(a => a.Name.Namespace == Xsl))
            return null;

        var result = root.Nodes().ToList();
        foreach (var n in result)
            n.Remove();
        return result;
    }

    private static List<XNode> Default(string noteText) =>
    [
        new XElement("b", "Not: "),
        new XElement(Xsl + "text", noteText),
        new XElement("br"),
    ];

    private static List<XNode> WrapForContext(List<XNode> nodes, NotesTarget target)
    {
        var parent = target.InsertParent.Name.Namespace == Xsl
            ? target.InsertParent.Ancestors().FirstOrDefault(a => a.Name.Namespace != Xsl)
            : target.InsertParent;
        var local = parent?.Name.LocalName.ToLowerInvariant() ?? "";
        var tops = nodes.OfType<XElement>().Where(e => e.Name.Namespace != Xsl).Select(e => e.Name.LocalName.ToLowerInvariant()).ToList();

        if (local is "table" or "tbody" or "thead" or "tfoot" && !tops.Contains("tr"))
            return [new XElement("tr", new XElement("td", nodes))];
        if (local == "tr" && !tops.Contains("td") && !tops.Contains("th"))
            return [new XElement("td", nodes)];
        if (local is "ul" or "ol" && !tops.Contains("li"))
            return [new XElement("li", nodes)];
        return nodes;
    }

    private static XNode Clone(XNode node) => node switch
    {
        XElement e => new XElement(e),
        XText t => new XText(t.Value),
        _ => new XText(string.Empty),
    };

    // Düğümleri xsl önekini koruyarak serialize edip dış sarmalayıcıyı sıyırır.
    private static string Serialize(IEnumerable<XNode> nodes)
    {
        var wrap = new XElement(Xsl + "wrap", new XAttribute(XNamespace.Xmlns + "xsl", Xsl.NamespaceName), nodes);
        var s = wrap.ToString(SaveOptions.DisableFormatting);
        var open = s.IndexOf('>') + 1;
        var close = s.LastIndexOf("</", StringComparison.Ordinal);
        return s[open..close].Trim();
    }

    private static string Collapse(string value) => Regex.Replace(value, @"\s+", string.Empty);
}
