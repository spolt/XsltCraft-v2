namespace XsltCraft.Application.Validation.Safety;

/// <summary>Token türleri. Boşluk ve <c>(: :)</c> yorumları token üretmez (ayıklanır).</summary>
internal enum XPathTokenKind
{
    Name,     // QName: NCName veya önek:yerel
    EqName,   // Q{uri}yerel
    Var,      // $QName
    String,   // '...' veya "..." (tırnak dışı; '' / "" kaçışı çözülmüş)
    Number,
    Hash,     // #
    LParen,
    RParen,
    Other,    // tek karakterlik operatör vb.
}

internal readonly struct XPathToken(XPathTokenKind kind, string text, string? uri = null)
{
    public XPathTokenKind Kind { get; } = kind;

    /// <summary>Name/Var için QName metni; EqName için yerel ad; String için literal içeriği.</summary>
    public string Text { get; } = text;

    /// <summary>Yalnız <see cref="XPathTokenKind.EqName"/>: <c>Q{...}</c> içindeki ad alanı URI'si.</summary>
    public string? Uri { get; } = uri;
}

/// <summary>
/// XPath ifadesini güvenlik analizi için token'lara ayırır. Regex <b>değil</b>: string literal
/// (<c>''</c>/<c>""</c> kaçışlı) ve iç içe <c>(: :)</c> yorumları doğru ayıklar; böylece literal/yorum
/// içindeki <c>doc(</c>, <c>{</c>, <c>function(</c> gibi diziler yanlış-pozitif üretmez ve
/// <c>function(: c :)(</c> gibi yorumla gizlenmiş saldırılar yakalanır.
/// </summary>
internal static class XPathTokenizer
{
    public static List<XPathToken> Tokenize(string expr)
    {
        var tokens = new List<XPathToken>(Math.Min(expr.Length, 256));
        int i = 0, n = expr.Length;

        while (i < n)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Yorum: (: ... :) — iç içe olabilir.
            if (c == '(' && i + 1 < n && expr[i + 1] == ':')
            {
                int depth = 1;
                i += 2;
                while (i < n && depth > 0)
                {
                    if (i + 1 < n && expr[i] == '(' && expr[i + 1] == ':') { depth++; i += 2; }
                    else if (i + 1 < n && expr[i] == ':' && expr[i + 1] == ')') { depth--; i += 2; }
                    else i++;
                }
                continue;
            }

            if (c == '\'' || c == '"')
            {
                i = ReadString(expr, i, c, out var value);
                tokens.Add(new XPathToken(XPathTokenKind.String, value));
                continue;
            }

            if (c == '(') { tokens.Add(new XPathToken(XPathTokenKind.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new XPathToken(XPathTokenKind.RParen, ")")); i++; continue; }
            if (c == '#') { tokens.Add(new XPathToken(XPathTokenKind.Hash, "#")); i++; continue; }

            if (c == '$')
            {
                int j = i + 1;
                int start = j;
                while (j < n && IsQNameChar(expr, j)) j++;
                tokens.Add(new XPathToken(XPathTokenKind.Var, expr.Substring(start, j - start)));
                i = j;
                continue;
            }

            // EQName: Q{uri}local
            if (c == 'Q' && i + 1 < n && expr[i + 1] == '{')
            {
                int uriStart = i + 2;
                int k = uriStart;
                while (k < n && expr[k] != '}') k++;
                string uri = expr.Substring(uriStart, Math.Max(0, k - uriStart));
                k = k < n ? k + 1 : k; // '}' atla
                int localStart = k;
                while (k < n && IsNcNameChar(expr[k])) k++;
                string local = expr.Substring(localStart, k - localStart);
                tokens.Add(new XPathToken(XPathTokenKind.EqName, local, uri));
                i = k;
                continue;
            }

            if (IsNameStart(c))
            {
                i = ReadQName(expr, i, out var name);
                tokens.Add(new XPathToken(XPathTokenKind.Name, name));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(expr[i + 1])))
            {
                int j = i;
                while (j < n && (char.IsDigit(expr[j]) || expr[j] == '.' || expr[j] == 'e' || expr[j] == 'E'
                       || ((expr[j] == '+' || expr[j] == '-') && j > i && (expr[j - 1] == 'e' || expr[j - 1] == 'E'))))
                    j++;
                tokens.Add(new XPathToken(XPathTokenKind.Number, expr.Substring(i, j - i)));
                i = j;
                continue;
            }

            tokens.Add(new XPathToken(XPathTokenKind.Other, c.ToString()));
            i++;
        }

        return tokens;
    }

    private static int ReadString(string expr, int i, char quote, out string value)
    {
        int n = expr.Length;
        var sb = new System.Text.StringBuilder();
        i++; // açılış tırnağı
        while (i < n)
        {
            char c = expr[i];
            if (c == quote)
            {
                if (i + 1 < n && expr[i + 1] == quote) { sb.Append(quote); i += 2; continue; } // '' / "" kaçışı
                i++; // kapanış
                break;
            }
            sb.Append(c);
            i++;
        }
        value = sb.ToString();
        return i;
    }

    private static int ReadQName(string expr, int i, out string name)
    {
        int n = expr.Length, start = i;
        while (i < n && IsNcNameChar(expr[i])) i++;
        // Önek ayıracı ':' — ama eksen '::' veya ':=' değil.
        if (i < n && expr[i] == ':' && i + 1 < n && expr[i + 1] != ':' && IsNameStart(expr[i + 1]))
        {
            i++; // ':'
            while (i < n && IsNcNameChar(expr[i])) i++;
        }
        name = expr.Substring(start, i - start);
        return i;
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsNcNameChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';

    private static bool IsQNameChar(string expr, int j)
    {
        char c = expr[j];
        if (IsNcNameChar(c)) return true;
        // önek ayıracı (eksen değil)
        return c == ':' && j + 1 < expr.Length && expr[j + 1] != ':' && IsNameStart(expr[j + 1]);
    }
}
