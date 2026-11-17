using System.Text.RegularExpressions;
using System.Xml.Linq;

using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Application.Xslt.NoteLoops;

/// <summary>
/// Belge başlığı notlarını (<c>Invoice/cbc:Note</c>, <c>DespatchAdvice/cbc:Note</c>) dolaşan <c>xsl:for-each</c>'leri
/// sınıflandırır: yol eşleşmesi (önek değil ad alanı URI'si), <b>genel</b> (filtresiz / yalnız dışlayıcı filtre) mi
/// <b>seçici</b> (belirli kodlu notu arayan) mı, ve görünür bir bölgede mi (değer üretimi, gizli kapsayıcı, içerik
/// koşulu, ölü şablon dışı). Sayfalama koşulları (<c>$index = $count</c>) görünürlük-nötr sayılır.
/// </summary>
internal sealed class NoteLoopClassifier
{
    public static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    private const string InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string DespatchNs = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    private const string CbcNs = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private const int MaxResolveDepth = 8;

    // Çıktıya doğrudan render edilmeyen XSL bağlamları.
    private static readonly HashSet<string> ValueContexts = new(StringComparer.Ordinal)
    {
        "variable", "param", "with-param", "attribute", "comment", "processing-instruction", "message",
        "sort", "key", "result-document", "for-each-group", "function", "iterate", "namespace",
    };

    // Görünür içerik taşımayan literal kapsayıcılar.
    private static readonly HashSet<string> HiddenLiterals = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "title", "script", "style", "noscript", "textarea", "option", "select",
    };

    // Nötr ifadede izin verilen fonksiyonlar (belgeye/bağlama erişmeyen).
    private static readonly HashSet<string> NeutralFunctions = new(StringComparer.Ordinal)
    {
        "true", "false", "not", "number", "boolean", "string", "concat", "ceiling", "floor", "round",
        "string-length", "substring", "contains", "starts-with", "normalize-space", "translate", "abs",
    };

    private static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        "and", "or", "div", "mod", "to", "eq", "ne", "lt", "le", "gt", "ge",
    };

    private readonly XElement _stylesheet;
    private readonly Dictionary<XElement, bool> _neutralVarMemo = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<XElement, bool> _reachableMemo = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _rootVars;

    public NoteLoopClassifier(XDocument doc)
    {
        _stylesheet = doc.Root ?? new XElement("empty");
        _rootVars = _stylesheet.Elements()
            .Where(e => e.Name == Xsl + "variable" || e.Name == Xsl + "param")
            .Where(e => Collapse((string?)e.Attribute("select") ?? "") == "/")
            .Select(e => (string?)e.Attribute("name"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Not döngüsü mü; öyleyse <paramref name="general"/> = filtresiz/yalnız dışlayıcı filtre.</summary>
    public bool IsHeaderNoteLoop(XElement forEach, out bool general)
    {
        general = false;
        if (forEach.Name != Xsl + "for-each" || forEach.Attribute("select") is not { } sel)
            return false;

        var t = XPathTokenizer.Tokenize(sel.Value, out var malformed);
        if (malformed || t.Count < 3)
            return false;

        var i = 0;
        // Bağlam ön eki: '' | '/' | '//' | $kök ('/' | '//')
        if (t[i].Kind == XPathTokenKind.Var)
        {
            if (!_rootVars.Contains(t[i].Text))
                return false;
            i++;
            if (!IsOther(t, i, "/"))
                return false;
        }
        while (IsOther(t, i, "/"))
            i++;
        if (i > 0 && t[0].Kind != XPathTokenKind.Var && i > 2)
            return false;

        // Kök adım (yüklemsiz) '/' Note
        if (!IsName(t, i) || ResolveName(forEach, t[i].Text) is not { } root
            || !(root == XName.Get("Invoice", InvoiceNs) || root == XName.Get("DespatchAdvice", DespatchNs)))
            return false;
        i++;
        if (!IsOther(t, i, "/"))
            return false;
        i++;
        if (!IsName(t, i) || ResolveName(forEach, t[i].Text) != XName.Get("Note", CbcNs))
            return false;
        i++;

        // Yalnız Note adımında yüklem(ler).
        general = true;
        while (i < t.Count)
        {
            if (!IsOther(t, i, "["))
                return false;
            var close = MatchBracket(t, i);
            if (close < 0)
                return false;
            general &= IsExclusionPredicate(t, i + 1, close);
            i = close + 1;
        }
        return true;
    }

    /// <summary>Döngü (veya yedek kapsayıcı) görünür ve her faturada render olan bir bölgede mi?</summary>
    public bool IsVisible(XElement e)
    {
        XElement? template = null;
        foreach (var a in e.AncestorsAndSelf())
        {
            if (a.Name.Namespace == Xsl)
            {
                var local = a.Name.LocalName;
                if (local == "template")
                {
                    template = a;
                    break;
                }
                if (ValueContexts.Contains(local))
                    return false;
                if (ReferenceEquals(a, e))
                    continue;
                switch (local)
                {
                    case "if" or "when" when !IsNeutral((string?)a.Attribute("test") ?? "", a, 0):
                        return false;
                    case "otherwise" when a.ElementsBeforeSelf(Xsl + "when")
                        .Any(w => !IsNeutral((string?)w.Attribute("test") ?? "", w, 0)):
                        return false;
                    case "for-each" when !IsRootContext((string?)a.Attribute("select") ?? "")
                                         && !IsNeutral((string?)a.Attribute("select") ?? "", a, 0):
                        return false;
                }
            }
            else if (IsHidden(a))
            {
                return false;
            }
        }

        if (template is null)
            return false;
        if (!_reachableMemo.TryGetValue(template, out var reachable))
            _reachableMemo[template] = reachable = IsReachable(template);
        return reachable;
    }

    // ── Görünürlük yardımcıları ───────────────────────────────────────────

    private static bool IsHidden(XElement literal)
    {
        if (HiddenLiterals.Contains(literal.Name.LocalName) || (string?)literal.Attribute("id") == "qrvalue")
            return true;
        var style = Collapse((string?)literal.Attribute("style") ?? "").ToLowerInvariant();
        return style.Contains("display:none", StringComparison.Ordinal)
               || style.Contains("visibility:hidden", StringComparison.Ordinal);
    }

    private bool IsReachable(XElement template)
    {
        var match = Collapse((string?)template.Attribute("match") ?? "");
        if (match.Length > 0)
        {
            if (template.Attribute("mode") is not null)
                return false;
            return match == "/" || Regex.IsMatch(match, @"^/?([\w.-]+:)?(Invoice|DespatchAdvice)$");
        }

        if ((string?)template.Attribute("name") is not { } name)
            return false;
        return _stylesheet.Descendants(Xsl + "call-template")
            .Any(c => (string?)c.Attribute("name") == name && !c.Ancestors().Any(a => ReferenceEquals(a, template)));
    }

    private bool IsRootContext(string select)
    {
        var s = Collapse(select);
        if (s is "/" or ".")
            return true;
        if (s.StartsWith('$') && _rootVars.Contains(s[1..]))
            return true;
        return Regex.IsMatch(s, @"^(//?)?([\w.-]+:)?(Invoice|DespatchAdvice)$");
    }

    /// <summary>
    /// Belge yolu/bağlam içermeyen ifade; değişkenler içeren adlı şablonun parametresi, sabit tanımlı değişken
    /// ya da sayfa sayısı ifadesine (<c>count(…InvoiceLine)</c> + aritmetik) çözülmeli.
    /// </summary>
    private bool IsNeutral(string expr, XElement scope, int depth)
    {
        if (depth > MaxResolveDepth)
            return false;
        var t = XPathTokenizer.Tokenize(expr, out var malformed);
        if (malformed || t.Count == 0)
            return false;

        for (var i = 0; i < t.Count; i++)
        {
            var tok = t[i];
            switch (tok.Kind)
            {
                case XPathTokenKind.String or XPathTokenKind.Number or XPathTokenKind.LParen or XPathTokenKind.RParen:
                    continue;
                case XPathTokenKind.Var:
                    if (!IsNeutralVariable(tok.Text, scope, depth))
                        return false;
                    continue;
                case XPathTokenKind.Name:
                    if (Operators.Contains(tok.Text))
                        continue;
                    if (i + 1 < t.Count && t[i + 1].Kind == XPathTokenKind.LParen)
                    {
                        if (tok.Text == "count" && SkipLineCount(t, i + 1) is var end and > 0)
                        {
                            i = end;
                            continue;
                        }
                        if (NeutralFunctions.Contains(tok.Text) && !(i + 2 < t.Count && t[i + 2].Kind == XPathTokenKind.RParen && IsContextFunction(tok.Text)))
                            continue;
                    }
                    return false;
                case XPathTokenKind.Other:
                    if (tok.Text is "/" or "." or "@" or ":" or "[" or "]" or "|")
                        return false;
                    // '*' yalnız bir işlenenin ardından çarpmadır; aksi halde ad testi (belge erişimi).
                    if (tok.Text == "*" && (i == 0 || t[i - 1].Kind is not (XPathTokenKind.Number
                            or XPathTokenKind.Var or XPathTokenKind.RParen or XPathTokenKind.String)))
                        return false;
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    // Argümansız çağrıldığında bağlam düğümünü okuyan fonksiyonlar.
    private static bool IsContextFunction(string name) =>
        name is "string" or "number" or "string-length" or "normalize-space" or "boolean";

    // count( [/|//] [önek:]Invoice / [önek:]InvoiceLine ) — yalnız kalem sayısı; bitiş RParen indeksi ya da -1.
    private static int SkipLineCount(List<XPathToken> t, int lparen)
    {
        var close = MatchParen(t, lparen);
        if (close < 0)
            return -1;
        string? last = null;
        for (var k = lparen + 1; k < close; k++)
        {
            if (t[k].Kind == XPathTokenKind.Name)
                last = t[k].Text;
            else if (!(t[k].Kind == XPathTokenKind.Other && t[k].Text == "/")
                     && !(t[k].Kind == XPathTokenKind.Var))
                return -1;
        }
        var local = last?[(last.IndexOf(':') + 1)..];
        return local is "InvoiceLine" or "DespatchLine" ? close : -1;
    }

    private bool IsNeutralVariable(string name, XElement scope, int depth)
    {
        // (a) İçeren adlı şablonun parametresi (sayfalama: $index, $count).
        var template = scope.AncestorsAndSelf(Xsl + "template").FirstOrDefault();
        if (template?.Attribute("name") is not null
            && template.Elements(Xsl + "param").Any(p => (string?)p.Attribute("name") == name))
            return true;

        // (b)/(c) Tanımı nötr olan değişken/parametre: önce şablon içi, sonra üst düzey.
        var def = template?.Descendants(Xsl + "variable").FirstOrDefault(v => (string?)v.Attribute("name") == name)
                  ?? _stylesheet.Elements()
                      .FirstOrDefault(e => (e.Name == Xsl + "variable" || e.Name == Xsl + "param")
                                           && (string?)e.Attribute("name") == name);
        if (def is null)
            return false;
        if (_neutralVarMemo.TryGetValue(def, out var cached))
            return cached;

        _neutralVarMemo[def] = false; // döngü koruması
        var result = def.Attribute("select") is { } sel
            ? IsNeutral(sel.Value, def, depth + 1)
            : !def.Elements().Any();
        _neutralVarMemo[def] = result;
        return result;
    }

    // ── Yüklem / yol yardımcıları ─────────────────────────────────────────

    /// <summary>Yüklem yalnız dışlayıcı mı: <c>not(…)</c> terimlerinin <c>and</c> birleşimi, <c>. != '…'</c> ya da konum.</summary>
    private static bool IsExclusionPredicate(List<XPathToken> t, int start, int end)
    {
        if (start >= end)
            return false;
        if (end - start == 1 && t[start].Kind == XPathTokenKind.Number)
            return true;

        var i = start;
        while (i < end)
        {
            if (t[i].Kind == XPathTokenKind.Name && t[i].Text == "not" && i + 1 < end && t[i + 1].Kind == XPathTokenKind.LParen)
            {
                var close = MatchParen(t, i + 1);
                if (close < 0 || close >= end)
                    return false;
                i = close + 1;
            }
            else if (IsOther(t, i, ".") && IsOther(t, i + 1, "!") && IsOther(t, i + 2, "=") && i + 3 < end
                     && t[i + 3].Kind == XPathTokenKind.String)
            {
                i += 4;
            }
            else if (t[i].Kind == XPathTokenKind.Name && t[i].Text == "position")
            {
                return true; // konum yüklemi
            }
            else
            {
                return false;
            }

            if (i == end)
                return true;
            if (!(t[i].Kind == XPathTokenKind.Name && t[i].Text == "and"))
                return false;
            i++;
        }
        return false;
    }

    private static XName? ResolveName(XElement scope, string qname)
    {
        var colon = qname.IndexOf(':');
        if (colon < 0)
            return null; // öneksiz ad XPath 1.0'da ad alanısız → UBL değil
        var ns = scope.GetNamespaceOfPrefix(qname[..colon]);
        return ns is null ? null : ns + qname[(colon + 1)..];
    }

    private static int MatchBracket(List<XPathToken> t, int open) => MatchPair(t, open, "[", "]");

    private static int MatchParen(List<XPathToken> t, int open)
    {
        var depth = 0;
        for (var k = open; k < t.Count; k++)
        {
            if (t[k].Kind == XPathTokenKind.LParen) depth++;
            else if (t[k].Kind == XPathTokenKind.RParen && --depth == 0) return k;
        }
        return -1;
    }

    private static int MatchPair(List<XPathToken> t, int open, string o, string c)
    {
        var depth = 0;
        for (var k = open; k < t.Count; k++)
        {
            if (IsOther(t, k, o)) depth++;
            else if (IsOther(t, k, c) && --depth == 0) return k;
        }
        return -1;
    }

    private static bool IsOther(List<XPathToken> t, int i, string text) =>
        i < t.Count && t[i].Kind == XPathTokenKind.Other && t[i].Text == text;

    private static bool IsName(List<XPathToken> t, int i) => i < t.Count && t[i].Kind == XPathTokenKind.Name;

    public static string Collapse(string value) => Regex.Replace(value, @"\s+", string.Empty);
}
