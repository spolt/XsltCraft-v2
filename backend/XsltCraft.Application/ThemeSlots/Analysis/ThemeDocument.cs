using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>İşaret elemanının serileşme biçimi: html çıktıda boş div yeterli; xml/xhtml çıktıda boş div
/// <c>&lt;div/&gt;</c> olur ve HTML ayrıştırıcı onu açılış etiketi sayıp sonraki içeriği yutar → yorum çocuk eklenir.</summary>
internal enum MarkerStyle { Plain, WithComment }

/// <summary>Analizin üzerinde çalıştığı tema: kaynak haritası + kök şablon + gövde + yardımcı sorgular.</summary>
internal sealed class ThemeDocument
{
    public static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>HTML boş (void) elemanları — içerik alamaz.</summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
    };

    public XsltSourceMap Map { get; }
    public DocumentType DocumentType { get; }
    public XElement RootTemplate { get; }
    public XElement Body { get; }
    public string XslPrefix { get; }
    public MarkerStyle MarkerStyle { get; }

    /// <summary><c>select="/"</c> ile tanımlı üst düzey değişkenler — <c>for-each select="$XML"</c> tek kez döner.</summary>
    public IReadOnlySet<string> SingleIterationVars { get; }

    private ThemeDocument(XsltSourceMap map, DocumentType type, XElement rootTemplate, XElement body,
        string xslPrefix, MarkerStyle markerStyle, IReadOnlySet<string> singleIterationVars)
    {
        Map = map;
        DocumentType = type;
        RootTemplate = rootTemplate;
        Body = body;
        XslPrefix = xslPrefix;
        MarkerStyle = markerStyle;
        SingleIterationVars = singleIterationVars;
    }

    public static ThemeDocument? TryCreate(XsltSourceMap map, DocumentType type)
    {
        var root = map.Document.Root;
        if (root is null || root.Name.Namespace != Xsl)
            return null;

        var templates = root.Elements(Xsl + "template").ToList();
        var rootTemplate =
            templates.FirstOrDefault(t => (string?)t.Attribute("match") == "/" && FindBody(t) is not null)
            ?? templates.FirstOrDefault(t => FindBody(t) is not null);
        if (rootTemplate is null)
            return null;

        var body = FindBody(rootTemplate)!;
        var prefix = root.GetPrefixOfNamespace(Xsl) ?? "xsl";

        var singleVars = root.Elements(Xsl + "variable")
            .Where(v => Collapse((string?)v.Attribute("select") ?? "") == "/")
            .Select(v => (string?)v.Attribute("name"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        return new ThemeDocument(map, type, rootTemplate, body, prefix, ResolveMarkerStyle(root, rootTemplate), singleVars);
    }

    private static XElement? FindBody(XElement template) =>
        template.Descendants().FirstOrDefault(e => IsLiteral(e, "body"));

    private static MarkerStyle ResolveMarkerStyle(XElement stylesheet, XElement rootTemplate)
    {
        var method = (string?)stylesheet.Element(Xsl + "output")?.Attribute("method");
        if (method is not null)
            return method.Trim().Equals("html", StringComparison.OrdinalIgnoreCase) ? MarkerStyle.Plain : MarkerStyle.WithComment;

        // method yoksa: ad alanı olmayan bir html kök sonuç elemanı → html çıktı yöntemi.
        var firstLiteral = rootTemplate.Descendants().FirstOrDefault(e => !IsXsl(e));
        return firstLiteral is not null && firstLiteral.Name.Namespace == XNamespace.None
               && firstLiteral.Name.LocalName.Equals("html", StringComparison.OrdinalIgnoreCase)
            ? MarkerStyle.Plain
            : MarkerStyle.WithComment;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    public static bool IsXsl(XElement e) => e.Name.Namespace == Xsl;

    public static bool IsXsl(XElement e, string localName) => e.Name.Namespace == Xsl && e.Name.LocalName == localName;

    /// <summary>Literal (sonuç) eleman — XHTML varsayılan ad alanı da olabileceği için yalnız yerel ada bakılır.</summary>
    public static bool IsLiteral(XElement e, string localName) =>
        e.Name.Namespace != Xsl && e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase);

    public static bool IsLiteral(XElement e) => e.Name.Namespace != Xsl;

    public static string? LiteralId(XElement e) => IsLiteral(e) ? (string?)e.Attribute("id") : null;

    /// <summary>En yakın literal ata (kendisi hariç), XSL sarmalayıcılarının üzerinden geçerek.</summary>
    public static XElement? LiteralParent(XElement e) => e.Ancestors().FirstOrDefault(IsLiteral);

    public static XElement? LiteralAncestor(XElement e, string localName) =>
        e.Ancestors().FirstOrDefault(a => IsLiteral(a, localName));

    public static XElement? LiteralAncestorOrSelf(XElement e, string localName) =>
        e.AncestorsAndSelf().FirstOrDefault(a => IsLiteral(a, localName));

    /// <summary>Bir literal elemanın literal çocukları — XSL sarmalayıcıları (if, choose, for-each…) açılarak.</summary>
    public static IEnumerable<XElement> LiteralChildren(XElement e)
    {
        foreach (var child in e.Elements())
        {
            if (IsLiteral(child))
            {
                yield return child;
            }
            else if (child.Name.LocalName is "if" or "choose" or "when" or "otherwise" or "for-each")
            {
                foreach (var nested in LiteralChildren(child))
                    yield return nested;
            }
        }
    }

    /// <summary>Elemanın içeriği yalnız boşluk/yorum (ve boş <c>xsl:comment</c>) mu?</summary>
    public static bool HasOnlyBlankContent(XElement e) => e.Nodes().All(n => n switch
    {
        XText t => string.IsNullOrWhiteSpace(t.Value),
        XComment => true,
        XElement c => IsXsl(c, "comment") && !c.Nodes().Any(),
        _ => false,
    });

    /// <summary>HTML boş (void) literal eleman mı (<c>img</c>, <c>br</c>, <c>hr</c> …)? İçeriği hep "boş" görünür ama
    /// içine bir şey konamaz — tutucu ya da hedef sayılmamalı.</summary>
    public static bool IsVoidElement(XElement e) => IsLiteral(e) && VoidElements.Contains(e.Name.LocalName);

    public bool IsInBody(XElement e) => ReferenceEquals(e, Body) || e.Ancestors().Any(a => ReferenceEquals(a, Body));

    /// <summary>Çıktıya doğrudan gitmeyen ya da dokunulmaması gereken bölge: script/style/head/title ve QR verisi.</summary>
    public static bool IsExcluded(XElement e) => e.AncestorsAndSelf().Any(a =>
        IsLiteral(a, "script") || IsLiteral(a, "style") || IsLiteral(a, "head") || IsLiteral(a, "title")
        || (IsLiteral(a) && (string?)a.Attribute("id") == "qrvalue"));

    public static string Collapse(string value) => Regex.Replace(value, @"\s+", string.Empty);
}
