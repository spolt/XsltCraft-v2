using System.Text.RegularExpressions;

namespace XsltCraft.Xslt.Saxon.Diagnostics;

/// <summary>
/// Saxon/IKVM hata metinlerinden iç ayrıntıyı temizler: Java/IKVM sınıf adları (<c>cli.</c>, <c>java.</c>,
/// <c>net.sf.saxon.</c>, <c>org.xml.sax.</c> …), "Caused by …" kuyrukları, URI'ler ve dosya yolları.
/// Kullanıcının kendi stylesheet'i için anlamlı kısım (hata kodu, ifade, satır) korunur.
/// </summary>
internal static partial class SaxonMessageSanitizer
{
    private const int MaxLength = 1_000;
    private const string StylesheetSystemId = "urn:xsltcraft:stylesheet";
    private const string InputSystemId = "urn:xsltcraft:input";

    public static string Clean(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "XSLT işlenemedi.";

        var s = message.Replace('\r', ' ').Replace('\n', ' ');
        s = CausedBy().Replace(s, string.Empty);
        s = s.Replace(StylesheetSystemId, "stylesheet", StringComparison.Ordinal)
             .Replace(InputSystemId, "girdi", StringComparison.Ordinal);
        s = Uri().Replace(s, "<uri>");
        s = WindowsPath().Replace(s, "<yol>");
        s = UncPath().Replace(s, "<yol>");
        s = UnixPath().Replace(s, "<yol>");
        s = JavaClassName().Replace(s, string.Empty);
        s = ExceptionPrefix().Replace(s, string.Empty);
        s = Whitespace().Replace(s, " ").Trim(' ', ':', '.', ',', ';');

        if (s.Length == 0)
            return "XSLT işlenemedi.";
        return s.Length <= MaxLength ? s : s[..MaxLength] + "…";
    }

    /// <summary>Temizlenmiş metinde yasak iç ayrıntı kaldı mı (test/savunma).</summary>
    public static bool LooksLeaky(string message) =>
        JavaClassName().IsMatch(message) || Uri().IsMatch(message) || WindowsPath().IsMatch(message) ||
        message.Contains("cli.", StringComparison.Ordinal);

    [GeneratedRegex(@"\s*Caused by\b.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CausedBy();

    [GeneratedRegex(@"\b(?:file|https?|ftp|jar|classpath|data|zip|jrt|mailto|ldap|gopher|netdoc|urn(?!:xsltcraft))\s*:[^\s'""<>)}\]]*", RegexOptions.IgnoreCase)]
    private static partial Regex Uri();

    [GeneratedRegex(@"\b[A-Za-z]:[\\/][^\s'""<>)}\]]*")]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"\\\\[^\s'""<>)}\]]+")]
    private static partial Regex UncPath();

    [GeneratedRegex(@"(?<![\w.:{/])/(?:app|usr|home|tmp|etc|proc|root|var|opt|mnt|srv|dev|sys|bin|lib|run|workspace)(?:/[^\s'""<>)}\]]*)?")]
    private static partial Regex UnixPath();

    // Paket önekiyle başlayan nitelikli Java/IKVM adları (net.sf.saxon.trans.XPathException, cli.System.Foo …).
    [GeneratedRegex(@"\b(?:cli|java|javax|jdk|sun|com\.sun|net\.sf\.saxon|org\.xml\.sax|org\.w3c|org\.xmlresolver|org\.apache|ikvm|IKVM|XsltCraft)\.[\w.$]+")]
    private static partial Regex JavaClassName();

    [GeneratedRegex(@"^\s*(?:[A-Z]\w*(?:Exception|Error))\s*:\s*")]
    private static partial Regex ExceptionPrefix();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Whitespace();
}
