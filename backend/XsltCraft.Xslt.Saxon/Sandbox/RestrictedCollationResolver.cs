using net.sf.saxon;
using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// İzin listeli collation çözümleyici (<c>Configuration.setCollationURIResolver</c>). Saxon'un standart
/// çözümleyicisi <c>http://saxon.sf.net/collation?class=X</c> ile classpath'teki HERHANGİ bir sınıfı
/// yükleyip public sıfır-argümanlı kurucusunu çağırır (static initializer'lar çalışır) —
/// ALLOW_EXTERNAL_FUNCTIONS=false bunu kapatmaz (G1 security-reviewer). Statik tarama da
/// <c>compare(..., concat(...))</c> ile dinamik kurulan URI'yi göremez; bu yüzden kapı burasıdır.
///
/// İzin: codepoint, html-ascii-case-insensitive ve sorgusunda <c>%</c> / <c>class</c> olmayan UCA.
/// Diğer her URI için FIRLATIR (null dönmek Saxon'un varsayılanına düşmek demektir).
/// <c>%</c> reddi ayrıca IKVM0117'yi (AnyURIValue.decode → NoSuchMethodError) önler.
/// </summary>
internal sealed class RestrictedCollationResolver : CollationURIResolver
{
    public const string CodepointUri = "http://www.w3.org/2005/xpath-functions/collation/codepoint";
    public const string HtmlAsciiCaseInsensitiveUri = "http://www.w3.org/2005/xpath-functions/collation/html-ascii-case-insensitive";
    public const string UcaUri = "http://www.w3.org/2013/collation/UCA";

    private readonly CollationURIResolver _standard;

    public RestrictedCollationResolver(CollationURIResolver standard)
    {
        _standard = standard ?? throw new ArgumentNullException(nameof(standard));
    }

    public StringCollator resolve(string collationURI, Configuration config)
    {
        if (!IsAllowed(collationURI))
        {
            throw new SandboxViolationException(SandboxCodes.CollationDenied,
                "Bu collation kullanılamaz; yalnız codepoint ve '%' içermeyen UCA collation'ları desteklenir.");
        }

        return _standard.resolve(collationURI, config)
            ?? throw new SandboxViolationException(SandboxCodes.CollationDenied, "Collation çözümlenemedi.");
    }

    /// <summary>URI'nin izin listesinde olup olmadığı (büyük/küçük harf duyarlı; Saxon da öyle karşılaştırır).</summary>
    public static bool IsAllowed(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;

        if (uri is CodepointUri or HtmlAsciiCaseInsensitiveUri)
            return true;

        if (!uri.StartsWith(UcaUri, StringComparison.Ordinal))
            return false;

        var rest = uri[UcaUri.Length..];
        if (rest.Length == 0)
            return true;
        if (rest[0] != '?')
            return false;

        // Sorgu yalnız güvenli karakterler: key=value;key=value (UCA parametreleri: lang, strength, fallback…).
        var query = rest[1..];
        if (query.Contains('%') || query.Contains("class", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var c in query)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '=' or ';' or '-' or '_' or '.'))
                return false;
        }

        return true;
    }
}
