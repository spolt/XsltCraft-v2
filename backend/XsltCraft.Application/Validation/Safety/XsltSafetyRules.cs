namespace XsltCraft.Application.Validation.Safety;

/// <summary>
/// XsltSafety izin/yasak tabloları — saf veri. Scanner bu tablolara göre karar verir.
/// Değerler XSLT 2.0 komut kümesi ve bilinen saldırı vektörlerine göre sabittir.
/// </summary>
internal static class XsltSafetyRules
{
    // ── Ad alanları ────────────────────────────────────────────────────────
    public const string Xsl = "http://www.w3.org/1999/XSL/Transform";
    public const string Fn = "http://www.w3.org/2005/xpath-functions";
    public const string Xs = "http://www.w3.org/2001/XMLSchema";
    public const string XmlnsNs = "http://www.w3.org/2000/xmlns/";
    public const string XmlNs = "http://www.w3.org/XML/1998/namespace";
    public const string Msxsl = "urn:schemas-microsoft-com:xslt";
    public const string Exsl = "http://exslt.org/common";
    public const string Saxon = "http://saxon.sf.net/";
    public const string XInclude = "http://www.w3.org/2001/XInclude";
    public const string MathFn = "http://www.w3.org/2005/xpath-functions/math";
    public const string MapFn = "http://www.w3.org/2005/xpath-functions/map";
    public const string ArrayFn = "http://www.w3.org/2005/xpath-functions/array";
    public const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    public const string Err = "http://www.w3.org/2005/xqt-errors";

    /// <summary>
    /// Ayrılmış ad alanları (XSLT 2.0/3.0 §3.2 "reserved namespaces"): bunlarda xsl:function
    /// tanımlanamaz (XTSE0740) ve kullanıcı-fonksiyon izin kümesine asla girmez.
    /// </summary>
    public static readonly HashSet<string> ReservedNamespaces = new(StringComparer.Ordinal)
    {
        Xsl, Fn, Xs, Xsi, Err, XmlNs, XmlnsNs, MathFn, MapFn, ArrayFn,
    };

    // ── Sınırlar ───────────────────────────────────────────────────────────
    public const int MaxElementDepth = 256;
    public const int MaxExpressionLength = 8 * 1024;
    public const int MaxParenDepth = 64;
    public const int MaxFindings = 200;

    /// <summary>XML boşluk karakterleri (S üretimi) — öznitelik değeri kırpmada yalnız bunlar atılır.</summary>
    public static readonly char[] XmlWhitespace = [' ', '\t', '\r', '\n'];

    // ── xsl: eleman sınıflandırması ──────────────────────────────────────────

    /// <summary>XSLT 2.0 komut/bildirim kümesi (izinli). Burada olmayan xsl: elemanı tehdittir.</summary>
    public static readonly HashSet<string> AllowedXslElements = new(StringComparer.Ordinal)
    {
        "analyze-string", "apply-imports", "apply-templates", "attribute", "attribute-set",
        "call-template", "character-map", "choose", "comment", "copy", "copy-of", "decimal-format",
        "document", "element", "fallback", "for-each", "for-each-group", "function", "if", "key",
        "matching-substring", "message", "namespace", "namespace-alias", "non-matching-substring",
        "number", "otherwise", "output", "output-character", "param", "perform-sort",
        "preserve-space", "processing-instruction", "sequence", "sort", "strip-space", "stylesheet",
        "template", "text", "transform", "value-of", "variable", "when", "with-param",
    };

    /// <summary>Açıkça yasak xsl: elemanları (Security) — dış kaynak / DoS / sandbox kaçışı.</summary>
    public static readonly HashSet<string> ForbiddenXslElements = new(StringComparer.Ordinal)
    {
        "import", "include", "import-schema", "use-package", "package", "result-document",
        "source-document", "evaluate", "merge", "merge-source", "merge-key", "merge-action",
        "stream", "try", "catch",
    };

    /// <summary>Desteklenmeyen (XSLT 3.0) xsl: elemanları (Unsupported) — Execute modunda engel.</summary>
    public static readonly HashSet<string> UnsupportedXslElements = new(StringComparer.Ordinal)
    {
        "iterate", "map", "map-entry", "array", "mode", "accumulator", "accumulator-rule", "assert",
        "where-populated", "on-empty", "on-non-empty", "global-context-item", "context-item",
        "fork", "break", "next-iteration",
    };

    // ── Öznitelikler ─────────────────────────────────────────────────────────

    /// <summary>xsl: elemanlarında tam XPath ifadesi taşıyan öznitelikler.</summary>
    public static readonly HashSet<string> XPathAttributes = new(StringComparer.Ordinal)
    {
        "select", "test", "match", "use", "group-by", "group-adjacent", "group-starting-with",
        "group-ending-with", "value", "from", "count", "xpath", "initial-value", "context-item",
        "key", "predicate",
    };

    // ── Fonksiyon ad alanı izin listesi / yasak fonksiyonlar ───────────────────

    /// <summary>fn ad alanında yasak fonksiyonlar (dış kaynak / ortam / dinamik yürütme).</summary>
    public static readonly HashSet<string> ForbiddenFnFunctions = new(StringComparer.Ordinal)
    {
        "document", "doc", "doc-available", "unparsed-text", "unparsed-text-lines",
        "unparsed-text-available", "collection", "uri-collection", "environment-variable",
        "available-environment-variables", "json-doc", "parse-xml", "parse-xml-fragment",
        "transform", "load-xquery-module", "function-lookup",
    };

    /// <summary>
    /// .NET (XslCompiledTransform) yerleşik, yan etkisiz <c>msxsl:</c> fonksiyonları — yalnız dize/düğüm
    /// dönüştürür; dosya/ağ/betik erişimi yoktur. <c>msxsl:script</c> eleman olarak ayrıca yasaktır.
    /// </summary>
    public static readonly HashSet<string> AllowedMsxslFunctions = new(StringComparer.Ordinal)
    {
        "node-set", "format-date", "format-time", "string-compare", "utc", "local-name",
        "namespace-uri", "number",
    };

    /// <summary>EXSLT common (<c>exsl:</c>) yan etkisiz fonksiyonları.</summary>
    public static readonly HashSet<string> AllowedExslFunctions = new(StringComparer.Ordinal)
    {
        "node-set", "object-type",
    };
}
