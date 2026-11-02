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

    /// <summary>Yalnız <see cref="XPathTokenKind.EqName"/>: <c>Q{...}</c> içindeki (boşluk-normalize) ad alanı URI'si.</summary>
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
    public static List<XPathToken> Tokenize(string expr) => Tokenize(expr, out _);

    /// <summary>
    /// <paramref name="malformed"/> = true: kapanmamış string literal / yorum ya da belirsiz
    /// (kapanmayan veya tırnak / yorum içeren) <c>Q{…}</c> braced URI — çağıran fail-closed davranır.
    /// </summary>
    public static List<XPathToken> Tokenize(string expr, out bool malformed)
    {
        malformed = false;
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
            if (XPathLexer.IsCommentStart(expr, i))
            {
                i = XPathLexer.SkipComment(expr, i, out var commentClosed);
                malformed |= !commentClosed;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                int end = XPathLexer.SkipString(expr, i, out var stringClosed);
                malformed |= !stringClosed;
                tokens.Add(new XPathToken(XPathTokenKind.String, StringValue(expr, i, end, stringClosed)));
                i = end;
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
            if (XPathLexer.IsBracedUriStart(expr, i))
            {
                int uriStart = i + 2;
                int k = XPathLexer.FindBracedUriEnd(expr, i, out var uriClosed, out var hasBrace, out var ambiguous);
                malformed |= !uriClosed || hasBrace || ambiguous;
                // XPath 3.0: BracedURILiteral boşluk-normalize edilir (Saxon da kırpar) → Q{ fn }doc = fn:doc.
                string uri = XsltSafetyNamespaces.NormalizeNamespaceUri(expr.Substring(uriStart, k - uriStart));
                k = uriClosed ? k + 1 : k; // '}' atla
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

    /// <summary>Literal içeriği: tırnaklar hariç, <c>''</c>/<c>""</c> kaçışı çözülmüş.</summary>
    private static string StringValue(string expr, int start, int end, bool closed)
    {
        char quote = expr[start];
        int contentEnd = closed ? end - 1 : end;
        return expr.Substring(start + 1, contentEnd - start - 1).Replace(new string(quote, 2), quote.ToString());
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
