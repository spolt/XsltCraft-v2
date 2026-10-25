using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Application.Preview;

/// <summary><see cref="XPathPortability.Analyze"/> sonucu.</summary>
/// <param name="IsPortable">İfade iki motorda (.NET XPath 1.0 / Saxon XPath 2.0) aynı anlamda ve sınırlı maliyetle çalışır.</param>
/// <param name="IsNodeSet">Derlenmiş ifadenin dönüş tipi düğüm kümesi (tekil değer için <c>(x)[1]</c> sarmalanmalı).</param>
/// <param name="ErrorCode"><c>XPATH_TOO_LONG</c> / <c>XPATH_TOO_COMPLEX</c> / <c>XPATH_NOT_PORTABLE</c>; taşınabilirse <c>null</c>.</param>
/// <param name="Message">Türkçe açıklama; taşınabilirse <c>null</c>.</param>
public sealed record XPathPortabilityResult(bool IsPortable, bool IsNodeSet, string? ErrorCode, string? Message);

/// <summary>
/// Grid bağlama XPath'leri için "çift-motor güvenli altküme" kapısı (ADR A5 / G1): ifade XPath 1.0
/// fonksiyonlarıyla sınırlı olmalı, .NET'te derlenmeli ve maliyeti sınırlı olmalı (DoS kapısı).
/// Saf ve durumsuz; generator ve ileride v3 <c>BlockTreeValidator</c> çağırır.
/// <para>Sıra: ucuz ön kontroller (uzunluk, parantez/yüklem derinliği, maliyet sınıfı) derlemeden
/// <b>önce</b>; ardından fonksiyon beyaz listesi ve <see cref="XPathExpression.Compile(string, IXmlNamespaceResolver)"/>.</para>
/// </summary>
public static class XPathPortability
{
    public const int MaxLength = 512;
    public const int MaxParenDepth = 16;
    public const int MaxPredicateDepth = 3;

    public const string TooLong = "XPATH_TOO_LONG";
    public const string TooComplex = "XPATH_TOO_COMPLEX";
    public const string NotPortable = "XPATH_NOT_PORTABLE";

    /// <summary>XPath 1.0 çekirdek fonksiyonları + XSLT 1.0'ın iki motorda da aynı davranan üç fonksiyonu.</summary>
    private static readonly HashSet<string> PortableFunctions = new(StringComparer.Ordinal)
    {
        // XPath 1.0 §4
        "last", "position", "count", "id", "local-name", "namespace-uri", "name",
        "string", "concat", "starts-with", "contains", "substring-before", "substring-after",
        "substring", "string-length", "normalize-space", "translate",
        "boolean", "not", "true", "false", "lang",
        "number", "sum", "floor", "ceiling", "round",
        // XSLT 1.0 (yan etkisiz, bağlamsız)
        "format-number", "current", "generate-id",
    };

    /// <summary>Düğüm tipi testleri — fonksiyon çağrısı değil.</summary>
    private static readonly HashSet<string> NodeTypeTests = new(StringComparer.Ordinal)
    {
        "node", "text", "comment", "processing-instruction",
    };

    /// <summary>Düğüm kümesi döndüren fonksiyonlar (karşılaştırma işleneni sınıflandırması için).</summary>
    private static readonly HashSet<string> NodeSetFunctions = new(StringComparer.Ordinal) { "id", "current" };

    /// <summary>N-sınıfı (belge boyutunda) eksenler. <c>//</c> = descendant-or-self kısaltması ayrıca işlenir.</summary>
    private static readonly HashSet<string> NClassAxes = new(StringComparer.Ordinal)
    {
        "descendant", "descendant-or-self", "following", "preceding", "following-sibling", "preceding-sibling",
    };

    /// <summary>Yüklem içinde bağlam-göreli olsa da sınırsız (O(n)) kalan eksenler.</summary>
    private static readonly HashSet<string> UnboundedAxes = new(StringComparer.Ordinal)
    {
        "following", "preceding", "following-sibling", "preceding-sibling",
    };

    public static XPathPortabilityResult Analyze(string xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
            return Fail(NotPortable, "XPath boş.");
        if (xpath.Length > MaxLength)
            return Fail(TooLong, $"XPath {MaxLength} karakter sınırını aşıyor ({xpath.Length}).");

        var tokens = XPathTokenizer.Tokenize(xpath, out var malformed);
        if (malformed)
            return Fail(NotPortable, "XPath sözcüksel olarak hatalı (kapanmamış literal/yorum ya da Q{…}).");

        var structural = CheckStructure(tokens);
        if (structural is not null) return structural;

        var functions = CheckFunctions(tokens);
        if (functions is not null) return functions;

        XPathExpression compiled;
        try
        {
            compiled = XPathExpression.Compile(xpath, new PortabilityContext()); // durumlu NameTable → çağrı başına
        }
        catch (Exception ex) when (ex is XPathException or ArgumentException or XmlException)
        {
            return Fail(NotPortable, $"XPath 1.0 olarak derlenemedi: {ex.Message}");
        }

        return new XPathPortabilityResult(true, compiled.ReturnType == XPathResultType.NodeSet, null, null);
    }

    // ── Yapısal + maliyet kontrolleri ────────────────────────────────────

    private static XPathPortabilityResult? CheckStructure(List<XPathToken> t)
    {
        int paren = 0, pred = 0;
        for (int i = 0; i < t.Count; i++)
        {
            var tok = t[i];
            if (tok.Kind == XPathTokenKind.LParen && ++paren > MaxParenDepth)
                return Fail(TooComplex, $"Parantez derinliği {MaxParenDepth} sınırını aşıyor.");
            if (tok.Kind == XPathTokenKind.RParen) paren--;
            if (IsOther(tok, "[") && ++pred > MaxPredicateDepth)
                return Fail(TooComplex, $"İç içe yüklem derinliği {MaxPredicateDepth} sınırını aşıyor.");
            if (IsOther(tok, "]")) pred--;

            if (pred == 0) continue;

            // Yüklem içi N-sınıfı eksen kuralları
            if (IsDoubleSlash(t, i, out bool absolute))
            {
                if (pred > 1)
                    return Fail(TooComplex, "İç içe yüklemde '//' (N-sınıfı eksen) kullanılamaz.");
                if (absolute)
                    return Fail(TooComplex, "Yüklem içinde mutlak '//' yolu kullanılamaz (her düğüm için tüm belge taranır).");
            }
            else if (AxisName(t, i) is { } axis && NClassAxes.Contains(axis))
            {
                if (pred > 1)
                    return Fail(TooComplex, $"İç içe yüklemde '{axis}::' (N-sınıfı eksen) kullanılamaz.");
                if (UnboundedAxes.Contains(axis))
                    return Fail(TooComplex, $"Yüklem içinde '{axis}::' ekseni kullanılamaz (her düğüm için belge boyunda tarama).");
                if (i > 0 && IsOther(t[i - 1], "/") && !IsStepEnd(t, i - 2) && !(i > 1 && IsOther(t[i - 2], "/")))
                    return Fail(TooComplex, $"Yüklem içinde mutlak '/{axis}::' yolu kullanılamaz.");
            }
        }

        // Genel karşılaştırma: iki işlenen de düğüm kümesi ve en az biri N-sınıfı eksen taşıyorsa
        // maliyet O(|A|·|B|) (açık #2: //i[@v = //i/@w]).
        for (int i = 0; i < t.Count; i++)
        {
            if (!IsComparison(t, i, out int opLen)) continue;
            var (lNode, lN) = ClassifyOperand(t, i - 1, -1);
            var (rNode, rN) = ClassifyOperand(t, i + opLen, +1);
            if (lNode && rNode && (lN || rN))
                return Fail(TooComplex,
                    "N-sınıfı eksenli (//, descendant, following, preceding) bir düğüm kümesi başka bir düğüm kümesiyle " +
                    "karşılaştırılamaz (karesel maliyet).");
            i += opLen - 1;
        }

        return null;
    }

    /// <summary>
    /// Karşılaştırma işleneninin sınırı: <paramref name="from"/>'dan <paramref name="dir"/> yönünde, aynı
    /// iç içelik düzeyinde <c>and</c>/<c>or</c>/<c>,</c>/başka karşılaştırma ya da kapsayan parantez /
    /// köşeli parantez sınırına kadar. Dönen değer işlenenin son (dahil) token'ıdır.
    /// </summary>
    private static int OperandBound(List<XPathToken> t, int from, int dir)
    {
        int level = 0, i = from;
        for (; i >= 0 && i < t.Count; i += dir)
        {
            var tok = t[i];
            bool opens = dir > 0 ? IsOpen(tok) : IsClose(tok);
            bool closes = dir > 0 ? IsClose(tok) : IsOpen(tok);
            if (opens) { level++; continue; }
            if (closes)
            {
                if (level == 0) break;
                level--;
                continue;
            }
            if (level == 0 && (IsOther(tok, ",") || IsOperatorName(t, i, "and") || IsOperatorName(t, i, "or")
                               || IsComparison(t, i, out _) || IsComparisonTail(t, i)))
                break;
        }
        return i - dir;
    }

    /// <summary>Düğüm kümesi mi ve (fonksiyon argümanları dışında) N-sınıfı eksen var mı?</summary>
    private static (bool IsNodeSet, bool HasNAxis) ClassifyOperand(List<XPathToken> t, int from, int dir)
    {
        int bound = OperandBound(t, from, dir);
        return dir > 0 ? ClassifyRange(t, from, bound) : ClassifyRange(t, bound, from);
    }

    private static (bool, bool) ClassifyRange(List<XPathToken> t, int from, int to)
    {
        bool node = false, n = false;
        int fnDepth = 0, depth = 0;
        for (int i = from; i <= to && i < t.Count; i++)
        {
            var tok = t[i];
            if (IsOpen(tok))
            {
                depth++;
                if (fnDepth == 0 && tok.Kind == XPathTokenKind.LParen && IsFunctionCall(t, i - 1)
                    && !NodeSetFunctions.Contains(t[i - 1].Text))
                    fnDepth = depth;
                continue;
            }
            if (IsClose(tok))
            {
                if (fnDepth == depth) fnDepth = 0;
                depth--;
                continue;
            }
            if (fnDepth > 0) continue;
            if (IsPathToken(t, i)) node = true;
            if (IsDoubleSlash(t, i, out _) || (AxisName(t, i) is { } ax && NClassAxes.Contains(ax))) n = true;
        }
        return (node, n);
    }

    // ── Fonksiyon beyaz listesi ──────────────────────────────────────────

    private static XPathPortabilityResult? CheckFunctions(List<XPathToken> t)
    {
        for (int i = 0; i < t.Count; i++)
        {
            if (t[i].Kind == XPathTokenKind.EqName)
                return Fail(NotPortable, "Q{…} genişletilmiş ad XPath 1.0'da yok.");
            if (!IsFunctionCall(t, i)) continue;
            var name = t[i].Text;
            if (NodeTypeTests.Contains(name)) continue;
            if (!PortableFunctions.Contains(name))
                return Fail(NotPortable, $"'{name}()' XPath 1.0 fonksiyonu değil (iki motorda aynı sonuç garanti edilemez).");
        }
        return null;
    }

    // ── Token yardımcıları ───────────────────────────────────────────────

    private static bool IsOther(XPathToken t, string s) => t.Kind == XPathTokenKind.Other && t.Text == s;
    private static bool IsOpen(XPathToken t) => t.Kind == XPathTokenKind.LParen || IsOther(t, "[");
    private static bool IsClose(XPathToken t) => t.Kind == XPathTokenKind.RParen || IsOther(t, "]");

    /// <summary><paramref name="i"/>: ad + ardından <c>(</c> ve operatör konumunda değil → fonksiyon çağrısı (veya düğüm tipi testi).</summary>
    private static bool IsFunctionCall(List<XPathToken> t, int i) =>
        i >= 0 && i + 1 < t.Count
        && t[i].Kind == XPathTokenKind.Name
        && t[i + 1].Kind == XPathTokenKind.LParen
        && !IsOperatorPosition(t, i);

    /// <summary>XPath 1.0 §3.7: önceki token değer bitiren bir token ise ad bir operatördür (and/or/div/mod).</summary>
    private static bool IsOperatorPosition(List<XPathToken> t, int i) =>
        t[i].Text is "and" or "or" or "div" or "mod" && IsStepEnd(t, i - 1);

    private static bool IsOperatorName(List<XPathToken> t, int i, string op) =>
        t[i].Kind == XPathTokenKind.Name && t[i].Text == op && IsStepEnd(t, i - 1);

    /// <summary>Değer/adım bitiren token: ad, sayı, literal, değişken, <c>)</c>, <c>]</c>, <c>.</c>, <c>*</c>.</summary>
    private static bool IsStepEnd(List<XPathToken> t, int i)
    {
        if (i < 0) return false;
        var k = t[i];
        if (k.Kind is XPathTokenKind.Number or XPathTokenKind.String or XPathTokenKind.Var or XPathTokenKind.RParen)
            return true;
        if (k.Kind == XPathTokenKind.Name)
            return !IsOperatorPosition(t, i) && !(i + 1 < t.Count && IsOther(t[i + 1], ":"));
        return IsOther(k, "]") || IsOther(k, ".") || (IsOther(k, "*") && !IsStepEnd(t, i - 1));
    }

    /// <summary><paramref name="i"/> bir <c>//</c>'nin ilk <c>/</c>'si mi? <paramref name="absolute"/>: işlenen başında (bağlamdan bağımsız).</summary>
    private static bool IsDoubleSlash(List<XPathToken> t, int i, out bool absolute)
    {
        absolute = false;
        if (i + 1 >= t.Count || !IsOther(t[i], "/") || !IsOther(t[i + 1], "/")) return false;
        if (i > 0 && IsOther(t[i - 1], "/")) return false; // "///" ikinci eşleşme değil
        absolute = !IsStepEnd(t, i - 1);
        return true;
    }

    /// <summary><paramref name="i"/>: <c>eksen::</c> ise eksen adı.</summary>
    private static string? AxisName(List<XPathToken> t, int i) =>
        i + 2 < t.Count && t[i].Kind == XPathTokenKind.Name && IsOther(t[i + 1], ":") && IsOther(t[i + 2], ":")
            ? t[i].Text
            : null;

    /// <summary>Konum yolu parçası: ad (fonksiyon/operatör değil), <c>@</c>, <c>.</c>, <c>/</c>, joker <c>*</c>, değişken.</summary>
    private static bool IsPathToken(List<XPathToken> t, int i)
    {
        var k = t[i];
        if (k.Kind == XPathTokenKind.Var) return true;
        if (k.Kind == XPathTokenKind.Name)
            return !IsOperatorPosition(t, i) && !(IsFunctionCall(t, i) && !NodeTypeTests.Contains(k.Text));
        if (IsOther(k, "@") || IsOther(k, ".") || IsOther(k, "/")) return true;
        return IsOther(k, "*") && !IsStepEnd(t, i - 1);
    }

    /// <summary>Genel karşılaştırma: <c>=</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>.</summary>
    private static bool IsComparison(List<XPathToken> t, int i, out int length)
    {
        length = 0;
        if (t[i].Kind != XPathTokenKind.Other) return false;
        var s = t[i].Text;
        bool nextEq = i + 1 < t.Count && IsOther(t[i + 1], "=");
        switch (s)
        {
            case "!" when nextEq: length = 2; return true;
            case "<" or ">": length = nextEq ? 2 : 1; return true;
            case "=" when !IsComparisonTail(t, i): length = 1; return true;
            default: return false;
        }
    }

    /// <summary><c>!=</c>/<c>&lt;=</c>/<c>&gt;=</c>'nin ikinci karakteri olan <c>=</c>.</summary>
    private static bool IsComparisonTail(List<XPathToken> t, int i) =>
        IsOther(t[i], "=") && i > 0 && (IsOther(t[i - 1], "!") || IsOther(t[i - 1], "<") || IsOther(t[i - 1], ">"));

    private static XPathPortabilityResult Fail(string code, string message) => new(false, false, code, message);

    /// <summary>
    /// Derleme bağlamı: UBL önekleri (n1/cbc/cac/ext) + XSLT 1.0'ın üç taşınabilir fonksiyonu için
    /// dönüş tipi taşıyan saplamalar (yalnız derleme; hiçbir zaman çalıştırılmaz). Değişkenler
    /// <c>Any</c> tiplidir (generator bağlamında <c>$pos</c> gibi).
    /// </summary>
    private sealed class PortabilityContext : XsltContext
    {
        public PortabilityContext() : base(new NameTable())
        {
            AddNamespace("n1", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
            AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        }

        public override bool Whitespace => false;
        public override bool PreserveWhitespace(XPathNavigator node) => false;
        public override int CompareDocument(string baseUri, string nextbaseUri) => 0;

        public override IXsltContextFunction? ResolveFunction(string prefix, string name, XPathResultType[] argTypes) =>
            prefix.Length != 0 ? null : name switch
            {
                "format-number" => new StubFunction(2, 3, XPathResultType.String),
                "generate-id" => new StubFunction(0, 1, XPathResultType.String),
                "current" => new StubFunction(0, 0, XPathResultType.NodeSet),
                _ => null,
            };

        public override IXsltContextVariable ResolveVariable(string prefix, string name) => new StubVariable();
    }

    private sealed class StubFunction(int min, int max, XPathResultType returnType) : IXsltContextFunction
    {
        public int Minargs => min;
        public int Maxargs => max;
        public XPathResultType ReturnType => returnType;
        public XPathResultType[] ArgTypes => [];
        public object Invoke(XsltContext xsltContext, object[] args, XPathNavigator docContext) =>
            throw new NotSupportedException("Yalnız derleme bağlamı.");
    }

    private sealed class StubVariable : IXsltContextVariable
    {
        public bool IsLocal => false;
        public bool IsParam => false;
        public XPathResultType VariableType => XPathResultType.Any;
        public object Evaluate(XsltContext xsltContext) => throw new NotSupportedException("Yalnız derleme bağlamı.");
    }
}
