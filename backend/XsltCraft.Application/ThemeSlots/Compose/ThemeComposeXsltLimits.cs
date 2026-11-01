using System.Buffers;
using System.Xml;

using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// İstekle gelen <c>xslt</c> metninin birleştirme öncesi yapısal sınırları: <see cref="ThemeAssetComposer"/> metni API
/// sürecinde, iptalsiz ve süresiz işler (<c>XDocument</c>, ata taramaları). Denetim akış tabanlıdır; ağaç kurmaz, sınır
/// aşılınca hemen durur. Birleştirmenin kullandığı okuyucu ayarlarıyla okur: burada ayrıştırılamayan metin birleştirmede
/// de ağaca dönüşmez, bu yüzden iyi biçimli olmayan metin burada reddedilmez (bugünkü davranış sürer).
/// <para>Tavanlar repodaki temalara göre seçilmiştir (en büyüğü <c>invoice-newtemplate.xslt</c>, 385 KB): 3.077 eleman,
/// derinlik 21, etikette 23 öznitelik, 91 benzersiz ad, 26 karakter ad, 75 karakter ad alanı URI'si.</para>
/// </summary>
public static class ThemeComposeXsltLimits
{
    public const int MaxDepth = XsltSafetyRules.MaxElementDepth;

    /// <summary>En büyük temanın yaklaşık 5 katı. Gömülü base64 görsel metin düğümüdür, eleman sayısını artırmaz.</summary>
    public const int MaxElements = 16_000;

    /// <summary>
    /// Etiket başına öznitelik sayısı (değer uzunluğu değil; uzun <c>src</c> base64 meşrudur). Etiket
    /// <see cref="XmlReader.Read"/> içinde bütünüyle ayrıştırıldığı için okuyucudan önce metin üzerinde sayılır.
    /// </summary>
    public const int MaxAttributesPerElement = 256;

    /// <summary>
    /// Benzersiz nitelikli ad (ad alanı + yerel ad; eleman + öznitelik). <c>XName</c> tablosu adları süreç boyunca tutar;
    /// tavan istek başına büyümeyi sınırlar.
    /// </summary>
    public const int MaxUniqueNames = 512;

    /// <summary>Önek dahil ad uzunluğu.</summary>
    public const int MaxNameLength = 128;

    public const int MaxNamespaceUriLength = 256;

    /// <summary>XSLT 1.0/2.0 elemanları; yasak olanlar burada kalır, reddi <see cref="XsltSafetyScanner"/> açıklar.</summary>
    private static readonly HashSet<string> XslElementNames = new(XsltSafetyRules.AllowedXslElements, StringComparer.Ordinal)
    {
        "import", "include", "import-schema", "result-document",
    };

    /// <summary>XSLT 2.0'ın değişmez sonuç elemanlarındaki standart <c>xsl:</c> öznitelikleri.</summary>
    private static readonly HashSet<string> XslAttributeNames = new(StringComparer.Ordinal)
    {
        "default-collation", "exclude-result-prefixes", "extension-element-prefixes", "inherit-namespaces", "type",
        "use-attribute-sets", "use-when", "validation", "version", "xpath-default-namespace",
    };

    private static readonly SearchValues<char> TagDelimiters = SearchValues.Create("\"'=>");

    private static readonly ThemeComposeRequestError TooManyAttributes =
        new("xslt", $"XSLT bir elemanda çok fazla öznitelik içeriyor (en fazla {MaxAttributesPerElement}).");
    private static readonly ThemeComposeRequestError TooManyNames =
        new("xslt", $"XSLT çok fazla farklı eleman ve öznitelik adı içeriyor (en fazla {MaxUniqueNames}).");
    private static readonly ThemeComposeRequestError NameTooLong =
        new("xslt", $"XSLT çok uzun bir ad içeriyor (ad en fazla {MaxNameLength}, ad alanı en fazla {MaxNamespaceUriLength} karakter).");
    private static readonly ThemeComposeRequestError UnknownXslName =
        new("xslt", "XSLT, XSLT 1.0/2.0'da bulunmayan bir xsl: adı içeriyor.");

    public static ThemeComposeRequestError? Check(string xslt)
    {
        if (ExceedsAttributeLimit(xslt))
            return TooManyAttributes;

        var elements = 0;
        var names = new HashSet<(string Namespace, string Local)>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(xslt), SecureXslt.ReaderSettings());
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                // Depth 0 tabanlı (kök = 0) → seviye = Depth + 1 (XsltSafetyScanner ile aynı).
                if (reader.Depth + 1 > MaxDepth)
                    return new("xslt", $"XSLT eleman derinliği çok fazla (en fazla {MaxDepth} seviye).");
                if (++elements > MaxElements)
                    return new("xslt", $"XSLT çok fazla eleman içeriyor (en fazla {MaxElements}).");

                if (CheckName(reader, XslElementNames, names) is { } elementError)
                    return elementError;
                while (reader.MoveToNextAttribute())
                {
                    if (CheckName(reader, XslAttributeNames, names) is { } attributeError)
                        return attributeError;
                }
                reader.MoveToElement();
            }
        }
        catch (XmlException)
        {
            return null;
        }
        return null;
    }

    private static ThemeComposeRequestError? CheckName(XmlReader reader, HashSet<string> xslNames, HashSet<(string, string)> names)
    {
        string ns = reader.NamespaceURI, local = reader.LocalName;
        if (reader.Prefix.Length + 1 + local.Length > MaxNameLength || ns.Length > MaxNamespaceUriLength)
            return NameTooLong;
        if (XsltSafetyNamespaces.NormalizeNamespaceUri(ns) == XsltSafetyRules.Xsl && !xslNames.Contains(local))
            return UnknownXslName;

        names.Add((ns, local));
        return names.Count > MaxUniqueNames ? TooManyNames : null;
    }

    /// <summary>
    /// Doğrusal ön tarama: açılış etiketlerinde tırnak dışındaki <c>=</c> sayılır (iyi biçimli etikette öznitelik sayısına
    /// eşit). Metin, öznitelik değeri, yorum, CDATA ve PI sayılmaz. <c>&lt;!</c> ile başlayan diğer yapılar (DOCTYPE) ve
    /// yarım kalan yapılar taramayı bitirir: okuyucu o noktayı geçemez, kararı o verir.
    /// </summary>
    private static bool ExceedsAttributeLimit(ReadOnlySpan<char> rest)
    {
        while (true)
        {
            var open = rest.IndexOf('<');
            if (open < 0)
                return false;
            rest = rest[(open + 1)..];

            if (rest.StartsWith("!--"))
            {
                if (!TrySkipPast(ref rest, "-->", 3))
                    return false;
            }
            else if (rest.StartsWith("![CDATA["))
            {
                if (!TrySkipPast(ref rest, "]]>", 8))
                    return false;
            }
            else if (rest.StartsWith("!"))
            {
                return false;
            }
            else if (rest.StartsWith("?"))
            {
                if (!TrySkipPast(ref rest, "?>", 1))
                    return false;
            }
            else if (rest.StartsWith("/"))
            {
                if (!TrySkipPast(ref rest, ">", 1))
                    return false;
            }
            else
            {
                var attributes = 0;
                while (true)
                {
                    var next = rest.IndexOfAny(TagDelimiters);
                    if (next < 0)
                        return false;
                    var c = rest[next];
                    rest = rest[(next + 1)..];
                    if (c == '>')
                        break;
                    if (c == '=')
                    {
                        if (++attributes > MaxAttributesPerElement)
                            return true;
                        continue;
                    }

                    var close = rest.IndexOf(c);
                    if (close < 0)
                        return false;
                    rest = rest[(close + 1)..];
                }
            }
        }
    }

    private static bool TrySkipPast(ref ReadOnlySpan<char> rest, ReadOnlySpan<char> terminator, int openerLength)
    {
        rest = rest[openerLength..];
        var end = rest.IndexOf(terminator);
        if (end < 0)
            return false;
        rest = rest[(end + terminator.Length)..];
        return true;
    }
}
