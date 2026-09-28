using System.Text.RegularExpressions;

namespace XsltCraft.Application.Validation;

/// <summary>
/// XSLT/XPath içinde dış kaynak çağıran (SSRF / yerel dosya okuma) veya ortam sızdıran
/// fonksiyon ve referansları tespit eder.
///
/// Neden: kullanıcı binding XPath'i üretilen stylesheet'e doğrudan gömülür ve admin ham
/// tema (.xslt) yükleyebilir. Bu içerik render motoruna gider; <c>XsltTemplateRenderer</c>
/// içindeki Saxon yolu <c>document()</c>/<c>doc()</c>/<c>unparsed-text()</c> gibi
/// fonksiyonları varsayılan olarak çalıştırır. Bu yüzden içerik motora ulaşmadan önce
/// **fail-closed** taranır (hem generator çıktısı hem admin yüklemesi).
/// </summary>
public static class XsltSafety
{
    // document()/doc(): harici belge çekme; unparsed-text()/collection(): URI/dosya okuma;
    // system-property(): ortam sızıntısı. XPath 1.0/2.0 + XSLT karışık.
    private static readonly Regex ForbiddenFunction = new(
        @"(?<![\w-])(document|doc|doc-available|unparsed-text|unparsed-text-lines|unparsed-text-available|collection|uri-collection|system-property)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // XPath taşıyan attribute'lar — kullanıcı ifadeleri yalnız buralara girer.
    private static readonly Regex XPathAttr = new(
        "\\b(?:select|test|match|use|use-when)\\s*=\\s*\"([^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // <xsl:import|include> — href biçimi ne olursa olsun. Stylesheet'ler base URI'siz (string'den)
    // yüklendiği için meşru bir göreli include hiç çalışmaz; buna karşılık scheme'siz mutlak yol
    // (/etc/.., C:/..), UNC (\\sunucu\pay — NTLM sızıntısı) ve göreli yol (çalışma dizinine göre
    // çözülür, ../ ile gezinilir) dosya okumaya açılır. Motor tarafı SecureXslt ile zaten
    // çözümlemez; bu tarama erken ve açıklayıcı ret içindir.
    private static readonly Regex ExternalInclude = new(
        "<xsl:(?:import|include)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// XSLT içeriğinde tehdit bulursa açıklayıcı mesaj döndürür; güvenliyse <c>null</c>.
    /// Yalnız XPath taşıyan attribute'ları ve harici import/include'ları tarar — yorum/etiket
    /// metnindeki masum eşleşmeler yanlış-pozitif üretmez.
    /// </summary>
    public static string? FindThreat(string? xslt)
    {
        if (string.IsNullOrEmpty(xslt))
            return null;

        foreach (Match attr in XPathAttr.Matches(xslt))
        {
            var fn = ForbiddenFunction.Match(attr.Groups[1].Value);
            if (fn.Success)
                return $"Güvenli olmayan XPath fonksiyonu: {fn.Groups[1].Value.ToLowerInvariant()}()";
        }

        if (ExternalInclude.IsMatch(xslt))
            return "xsl:import / xsl:include desteklenmiyor — şablon tek, bağımsız bir dosya olmalı.";

        return null;
    }
}
