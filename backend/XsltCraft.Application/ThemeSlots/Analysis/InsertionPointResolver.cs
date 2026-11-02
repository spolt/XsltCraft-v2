using System.Xml.Linq;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Analysis;

internal enum PointKind { Before, After, ContentStart, ContentEnd }

/// <param name="Offset">Kaynak metindeki ekleme ofseti.</param>
/// <param name="Parent">Eklenen düğümün ebeveyn elemanı (XSL ya da literal).</param>
/// <param name="Conditional">Nokta bir xsl:if/when/otherwise içinde kaldı → yuva her faturada çıkmayabilir.</param>
internal sealed record InsertionPoint(int Offset, XElement Parent, bool Conditional);

/// <summary>
/// Hedef elemana göre işaret ekleme noktasını bulur ve geçerliliğini denetler:
/// <list type="bullet">
/// <item>Koşul sarmalayıcısının (if/when/otherwise) doğrudan içindeyse dışına çıkılır.</item>
/// <item>Tekrarlayan döngü, değer üreten yapı (variable/param/attribute…), script/style/QR içine eklenmez.
///   <c>for-each select="/" | "." | "$XML"</c> (tek kez döner) tekrarlayan sayılmaz — tüm GİB temalarının
///   gövdesi böyle bir döngünün içindedir.</item>
/// <item>HTML ebeveyni beyaz listede olmalı: tablo/satır/paragraf içine konan div'i HTML ayrıştırıcı başka
///   yere taşır (foster-parenting).</item>
/// </list>
/// </summary>
internal sealed class InsertionPointResolver(ThemeDocument d)
{
    private static readonly HashSet<string> BlockingXsl = new(StringComparer.Ordinal)
    {
        "variable", "param", "with-param", "attribute", "comment", "processing-instruction", "message",
        "element", "copy", "copy-of", "value-of", "text", "sort", "key", "number", "attribute-set",
    };

    private static readonly HashSet<string> RepeatingXsl = new(StringComparer.Ordinal) { "for-each", "for-each-group", "iterate" };

    private static readonly HashSet<string> AllowedHtmlParents = new(StringComparer.OrdinalIgnoreCase)
    {
        "td", "th", "div", "body", "center", "section", "header", "footer", "main", "article",
        "li", "dd", "blockquote", "form", "fieldset",
    };

    public InsertionPoint? Resolve(XElement target, PointKind kind)
    {
        var span = d.Map.Span(target);
        if (span.SelfClosing && kind is PointKind.ContentStart or PointKind.ContentEnd)
            return null;

        var (offset, parent) = kind switch
        {
            PointKind.Before => (span.Start, target.Parent),
            PointKind.After => (span.End, target.Parent),
            PointKind.ContentStart => (span.StartTagEnd, (XElement?)target),
            _ => (span.EndTagStart, (XElement?)target),
        };
        if (parent is null || !IsValid(parent))
            return null;

        var before = kind is PointKind.Before or PointKind.ContentStart;
        var current = new InsertionPoint(offset, parent, false);

        // Koşul sarmalayıcılarından dışarı çık; geçersiz bir yere düşersek son geçerli noktada kal.
        while (IsConditionalWrapper(current.Parent))
        {
            var wrapper = IsXsl(current.Parent, "if") ? current.Parent : current.Parent.Parent; // when/otherwise → choose
            if (wrapper?.Parent is null)
                break;

            var ws = d.Map.Span(wrapper);
            var next = new InsertionPoint(before ? ws.Start : ws.End, wrapper.Parent, false);
            if (!IsValid(next.Parent))
                break;
            current = next;
        }

        return current with { Conditional = current.Parent.AncestorsAndSelf().Any(IsConditionalWrapper) };
    }

    /// <summary>Ebeveyn içine bir literal div eklemek güvenli mi?</summary>
    public bool IsValid(XElement parent)
    {
        if (!IsValidContext(parent))
            return false;
        var html = IsLiteral(parent) ? parent : LiteralParent(parent);
        return html is not null && AllowedHtmlParents.Contains(html.Name.LocalName);
    }

    /// <summary>Eleman gövdede, tekrarlayan döngü / değer üreten yapı / script / QR dışında mı? (Mevcut bir
    /// elemana yalnız nitelik eklenecekse HTML ebeveyn beyaz listesi gerekmez.)</summary>
    public bool IsValidContext(XElement e)
    {
        if (!d.IsInBody(e) || IsExcluded(e))
            return false;

        foreach (var a in e.AncestorsAndSelf())
        {
            if (ReferenceEquals(a, d.RootTemplate))
                break;
            if (!IsXsl(a))
            {
                if ((string?)a.Attribute("id") == "qrcode")
                    return false;
                continue;
            }
            if (BlockingXsl.Contains(a.Name.LocalName))
                return false;
            if (RepeatingXsl.Contains(a.Name.LocalName) && !IsSingleIteration(a))
                return false;
        }
        return true;
    }

    /// <summary>Eleman bir koşul (if/when/otherwise) içinde mi — her faturada çıkmayabilir.</summary>
    public static bool IsConditional(XElement e) => e.Ancestors().Any(IsConditionalWrapper);

    private bool IsSingleIteration(XElement loop)
    {
        if (!IsXsl(loop, "for-each"))
            return false;
        var select = Collapse((string?)loop.Attribute("select") ?? "");
        return select is "/" or "."
               || (select.StartsWith('$') && d.SingleIterationVars.Contains(select[1..]));
    }

    private static bool IsConditionalWrapper(XElement e) =>
        IsXsl(e, "if") || IsXsl(e, "when") || IsXsl(e, "otherwise");
}
