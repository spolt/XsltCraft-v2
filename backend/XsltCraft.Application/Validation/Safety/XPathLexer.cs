namespace XsltCraft.Application.Validation.Safety;

/// <summary>
/// XPath sözcüksel atlama yardımcıları — <see cref="XPathTokenizer"/> ve AVT taraması
/// (<c>{ … }</c> sınırı bulma) <b>aynı</b> kuralları kullansın diye tek yerde toplanır:
/// string literal (<c>''</c>/<c>""</c> kaçışlı), iç içe <c>(: … :)</c> yorumu ve
/// <c>Q{uri}</c> braced URI literal'i. Her yardımcı kapanmamış yapıyı bildirir; çağıran fail-closed
/// davranır (Security bulgusu).
/// </summary>
internal static class XPathLexer
{
    /// <summary><paramref name="i"/> konumunda <c>(:</c> yorum açılışı var mı?</summary>
    public static bool IsCommentStart(string s, int i) =>
        i + 1 < s.Length && s[i] == '(' && s[i + 1] == ':';

    /// <summary>
    /// <paramref name="i"/> <c>(:</c> üzerindeyken iç içe yorumu atlar; eşleşen <c>:)</c> sonrasını döndürür.
    /// Kapanmamışsa <paramref name="closed"/> = false ve dize sonu döner.
    /// </summary>
    public static int SkipComment(string s, int i, out bool closed)
    {
        int n = s.Length, depth = 1;
        i += 2;
        while (i < n)
        {
            if (i + 1 < n && s[i] == '(' && s[i + 1] == ':') { depth++; i += 2; }
            else if (i + 1 < n && s[i] == ':' && s[i + 1] == ')')
            {
                i += 2;
                if (--depth == 0) { closed = true; return i; }
            }
            else i++;
        }
        closed = false;
        return n;
    }

    /// <summary>
    /// <paramref name="i"/> açılış tırnağı üzerindeyken string literal'i atlar (<c>''</c>/<c>""</c>
    /// kaçışı literal içinde kalır); kapanış tırnağı sonrasını döndürür.
    /// </summary>
    public static int SkipString(string s, int i, out bool closed)
    {
        int n = s.Length;
        char quote = s[i];
        i++;
        while (i < n)
        {
            if (s[i] == quote)
            {
                if (i + 1 < n && s[i + 1] == quote) { i += 2; continue; }
                closed = true;
                return i + 1;
            }
            i++;
        }
        closed = false;
        return n;
    }

    /// <summary><paramref name="i"/> konumunda <c>Q{</c> braced URI literal açılışı var mı?</summary>
    public static bool IsBracedUriStart(string s, int i) =>
        i + 1 < s.Length && s[i] == 'Q' && s[i + 1] == '{';

    /// <summary>
    /// <paramref name="i"/> <c>Q{</c> üzerindeyken braced URI literal'in (<c>[^{}]*</c>) kapanış
    /// <c>}</c> konumunu döndürür (yoksa dize sonu; <paramref name="closed"/> = false).
    /// <paramref name="hasBrace"/>: içerikte <c>{</c> var (geçerli braced URI değil).
    /// <paramref name="ambiguous"/>: içerik tırnak / <c>(:</c> taşıyor — "braced URI" ile "iç içe süslü
    /// parantez + literal/yorum" okumaları farklı <c>}</c> sınırı verir (ayrıştırıcı farkı).
    /// İçerik bunlardan arınmışsa iki okuma da aynı sınırı verir.
    /// </summary>
    public static int FindBracedUriEnd(string s, int i, out bool closed, out bool hasBrace, out bool ambiguous)
    {
        int n = s.Length, k = i + 2;
        hasBrace = ambiguous = false;
        while (k < n && s[k] != '}')
        {
            char c = s[k];
            if (c == '{') hasBrace = true;
            else if (c == '\'' || c == '"' || IsCommentStart(s, k)) ambiguous = true;
            k++;
        }
        closed = k < n;
        return k;
    }

    /// <summary>
    /// AVT / değer şablonu içindeki gömülü ifadenin sonunu bulur. <paramref name="start"/> açılış
    /// <c>{</c>'den sonraki konumdur; dönen değer eşleşen kapanış <c>}</c>'nin konumudur (bulunamazsa
    /// dize sonu ve <paramref name="malformed"/> = true). String literal, iç içe yorum ve braced URI
    /// içindeki <c>}</c> ifadeyi bitirmez; ifade içi <c>{ }</c> (map / satır içi fonksiyon gövdesi)
    /// iç içe sayılır — XPath ayrıştırıcısının (Saxon) davranışıyla aynı.
    /// </summary>
    public static int FindEnclosedExpressionEnd(string s, int start, out bool malformed)
    {
        int n = s.Length, depth = 1, j = start;
        malformed = false;
        while (j < n)
        {
            char c = s[j];
            if (c == '\'' || c == '"')
            {
                j = SkipString(s, j, out var closed);
                if (!closed) break;
                continue;
            }
            if (IsCommentStart(s, j))
            {
                j = SkipComment(s, j, out var closed);
                if (!closed) break;
                continue;
            }
            if (IsBracedUriStart(s, j))
            {
                int close = FindBracedUriEnd(s, j, out var closed, out var hasBrace, out var ambiguous);
                if (closed && !hasBrace)
                {
                    malformed |= ambiguous; // tırnak / yorum içeren braced URI → belirsiz sınır
                    j = close + 1;
                    continue;
                }
                // Geçerli braced URI değil: 'Q' sıradan karakter, ardından gelen '{' iç içe sayılır.
            }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return j;
            j++;
        }
        malformed = true;
        return n;
    }
}
