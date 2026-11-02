using System.Text;
using System.Xml;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.Validation.Safety;

/// <summary>
/// XsltSafety ad alanı sınıflandırması: yasak (uzantı / reflexive) ad alanları, ayrılmış ad alanları
/// ve stylesheet'te tanımlı xsl:function ad alanlarının (ileri referanslar için) toplanması.
/// </summary>
internal static class XsltSafetyNamespaces
{
    /// <summary>
    /// Ad alanı URI'si boşluk normalizasyonu: XML boşlukları (space, tab, CR, LF) kırpılır, iç boşluk
    /// dizileri tek boşluğa indirilir. Saxon <c>Q{ uri }</c> ve ad alanı URI'lerini kırptığı için
    /// <b>tüm</b> sınıflandırma (yasak / ayrılmış / fn / xs / msxsl / exsl / …) normalize değerle yapılır;
    /// aksi halde <c>" java:…"</c> gibi bir URI yasak sayılmadan kullanıcı-fonksiyon kümesine girerdi.
    /// </summary>
    public static string NormalizeNamespaceUri(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
            return "";
        if (ns.AsSpan().IndexOfAny(XsltSafetyRules.XmlWhitespace) < 0)
            return ns;

        var sb = new StringBuilder(ns.Length);
        bool pendingSpace = false;
        foreach (char c in ns)
        {
            if (Array.IndexOf(XsltSafetyRules.XmlWhitespace, c) >= 0)
            {
                pendingSpace = sb.Length > 0; // baştaki boşluk atılır, sondaki hiç yazılmaz
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Öneki çözer ve URI'yi normalize eder; bildirilmemiş önek → <c>null</c>.</summary>
    public static string? Lookup(IXmlNamespaceResolver resolver, string prefix)
    {
        var ns = resolver.LookupNamespace(prefix);
        return ns == null ? null : NormalizeNamespaceUri(ns);
    }

    /// <summary>
    /// Saxon uzantıları (<c>http://saxon.sf.net/</c> ve alt URI'leri) ve reflexive Java / .NET bağlama
    /// ad alanları. Büyük/küçük harf duyarsız (fail-closed: şema adı varyantları da yakalanır).
    /// </summary>
    public static bool IsForbidden(string ns)
    {
        ns = NormalizeNamespaceUri(ns);
        return ns.StartsWith(XsltSafetyRules.Saxon, StringComparison.OrdinalIgnoreCase)
               || ns.StartsWith("java:", StringComparison.OrdinalIgnoreCase)
               || ns.StartsWith("clitype:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>XSLT/XPath ayrılmış ad alanları (fn, xs, xsl, math, map, array, …).</summary>
    public static bool IsReserved(string ns)
    {
        ns = NormalizeNamespaceUri(ns);
        return XsltSafetyRules.ReservedNamespaces.Contains(ns)
               || ns.StartsWith(XsltSafetyRules.Fn + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Kullanıcı fonksiyonlarına ayrılamayacak ad alanları: yasak + ayrılmış + yerleşik fonksiyonları
    /// ayrıca izin listesiyle denetlenen msxsl / exsl. Normalizasyondan sonra hâlâ boşluk içeren URI
    /// de reddedilir (fail-closed: işlemcinin iç boşluğu nasıl yorumlayacağı belirsiz).
    /// </summary>
    private static bool CanHostUserFunctions(string ns)
    {
        ns = NormalizeNamespaceUri(ns);
        return ns.Length > 0
               && !ns.Any(char.IsWhiteSpace)
               && !IsForbidden(ns)
               && !IsReserved(ns)
               && ns != XsltSafetyRules.Msxsl
               && ns != XsltSafetyRules.Exsl;
    }

    /// <summary>
    /// xsl:function/@name değerinin (normalize) ad alanı: <c>önek:yerel</c> (öneki <paramref name="lookup"/>
    /// ile çözer) veya <c>Q{uri}yerel</c>. Öneksiz / çözülemeyen ad → <c>null</c>.
    /// </summary>
    public static string? ResolveFunctionNamespace(string name, Func<string, string?> lookup)
    {
        name = name.Trim(XsltSafetyRules.XmlWhitespace);
        if (name.StartsWith("Q{", StringComparison.Ordinal))
        {
            int close = name.IndexOf('}');
            return close > 1 ? NormalizeNamespaceUri(name[2..close]) : null;
        }
        int colon = name.IndexOf(':');
        if (colon <= 0)
            return null;
        var ns = lookup(name[..colon]);
        return ns == null ? null : NormalizeNamespaceUri(ns);
    }

    /// <summary>
    /// Ön geçiş: xsl:function bildirimlerinin ad alanlarını toplar (ileri referanslar için).
    /// Yasak, ayrılmış, msxsl ve exsl ad alanları kümeye <b>asla</b> girmez — böylece bir xsl:function
    /// tanımı yasak bir ad alanını izin listesine sokamaz.
    /// </summary>
    public static HashSet<string> CollectUserFunctionNamespaces(string xslt)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xslt), SecureXslt.ReaderSettings());
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element
                    || NormalizeNamespaceUri(reader.NamespaceURI) != XsltSafetyRules.Xsl
                    || reader.LocalName != "function")
                    continue;

                var name = reader.GetAttribute("name");
                if (string.IsNullOrEmpty(name))
                    continue;
                var ns = ResolveFunctionNamespace(name, reader.LookupNamespace);
                if (ns != null && CanHostUserFunctions(ns))
                    result.Add(ns);
            }
        }
        catch (XmlException)
        {
            // Ana geçiş zaten fail-closed raporlayacak; burada sessiz kal.
        }
        return result;
    }
}
