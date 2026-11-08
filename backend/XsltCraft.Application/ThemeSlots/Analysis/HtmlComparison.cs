using System.Text;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>
/// Render çıktısını karşılaştırma için normalleştirir: etiket (<c>&lt;…&gt;</c>) çevresindeki boşluk silinir, kalan
/// boşluk dizileri tek boşluğa iner, baştaki/sondaki boşluk kırpılır. Girintili çıktı (html yönteminin varsayılanı)
/// işaretin çevresine satır sonu ekler; karşılaştırma bundan bağımsızdır. Tek geçişli tarama: süre girdiyle doğrusal.
/// </summary>
internal static class HtmlComparison
{
    public static string Normalize(string html) => CollapseSpaces(TrimAroundTags(html));

    // Etiket: bir '<' ile ondan sonraki ilk '>' arası. Son '>'dan sonraki '<' kapanmaz → oradan sonrası olduğu gibi kalır.
    private static string TrimAroundTags(string s)
    {
        var lastClose = s.LastIndexOf('>');
        var sb = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var open = s.IndexOf('<', i);
            if (open < 0 || open > lastClose)
            {
                sb.Append(s, i, s.Length - i);
                break;
            }

            var close = s.IndexOf('>', open + 1);
            var textEnd = open;
            while (textEnd > i && char.IsWhiteSpace(s[textEnd - 1]))
                textEnd--;
            sb.Append(s, i, textEnd - i).Append(s, open, close - open + 1);

            i = close + 1;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }
        return sb.ToString();
    }

    private static string CollapseSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pendingSpace = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace)
                sb.Append(' ');
            pendingSpace = false;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
