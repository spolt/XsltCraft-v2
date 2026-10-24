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
    /// Saxon uzantıları (<c>http://saxon.sf.net/</c> ve alt URI'leri) ve reflexive Java / .NET bağlama
    /// ad alanları. Büyük/küçük harf duyarsız (fail-closed: şema adı varyantları da yakalanır).
    /// </summary>
    public static bool IsForbidden(string ns) =>
        ns.StartsWith(XsltSafetyRules.Saxon, StringComparison.OrdinalIgnoreCase)
        || ns.StartsWith("java:", StringComparison.OrdinalIgnoreCase)
        || ns.StartsWith("clitype:", StringComparison.OrdinalIgnoreCase);

    /// <summary>XSLT/XPath ayrılmış ad alanları (fn, xs, xsl, math, map, array, …).</summary>
    public static bool IsReserved(string ns) =>
        XsltSafetyRules.ReservedNamespaces.Contains(ns)
        || ns.StartsWith(XsltSafetyRules.Fn + "/", StringComparison.Ordinal);

    /// <summary>
    /// Kullanıcı fonksiyonlarına ayrılamayacak ad alanları: yasak + ayrılmış + yerleşik fonksiyonları
    /// ayrıca izin listesiyle denetlenen msxsl / exsl.
    /// </summary>
    private static bool CanHostUserFunctions(string ns) =>
        ns.Length > 0
        && !IsForbidden(ns)
        && !IsReserved(ns)
        && ns != XsltSafetyRules.Msxsl
        && ns != XsltSafetyRules.Exsl;

    /// <summary>
    /// xsl:function/@name değerinin ad alanı: <c>önek:yerel</c> (öneki <paramref name="lookup"/> ile
    /// çözer) veya <c>Q{uri}yerel</c>. Öneksiz / çözülemeyen ad → <c>null</c>.
    /// </summary>
    public static string? ResolveFunctionNamespace(string name, Func<string, string?> lookup)
    {
        name = name.Trim(XsltSafetyRules.XmlWhitespace);
        if (name.StartsWith("Q{", StringComparison.Ordinal))
        {
            int close = name.IndexOf('}');
            return close > 1 ? name[2..close] : null;
        }
        int colon = name.IndexOf(':');
        return colon > 0 ? lookup(name[..colon]) : null;
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
                    || reader.NamespaceURI != XsltSafetyRules.Xsl
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
