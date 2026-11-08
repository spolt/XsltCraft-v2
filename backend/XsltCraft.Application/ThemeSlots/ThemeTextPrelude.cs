using System.Text.RegularExpressions;

namespace XsltCraft.Application.ThemeSlots;

/// <summary>Yüklenen XSLT metninin analizden önceki ortak ön işlemi: BOM temizleme ve kodlama bildirimi denetimi.</summary>
internal static partial class ThemeTextPrelude
{
    [GeneratedRegex(@"^\s*<\?xml[^>]*\bencoding\s*=\s*[""']([^""']+)[""']", RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredEncoding();

    public static string StripBom(string xslt) => xslt.Length > 0 && xslt[0] == '\uFEFF' ? xslt[1..] : xslt;

    /// <summary>XML bildirimindeki kodlama UTF-8 değilse adı (bildirimdeki haliyle); bildirim yoksa ya da UTF-8 ise null.</summary>
    public static string? NonUtf8DeclaredEncoding(string xslt)
    {
        var match = DeclaredEncoding().Match(xslt);
        if (!match.Success)
            return null;
        var name = match.Groups[1].Value;
        return name.Trim().Equals("utf-8", StringComparison.OrdinalIgnoreCase) ? null : name;
    }
}
