using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Tests.Safety;

/// <summary>
/// <see cref="XPathTokenizer"/> — string literal (''/"" kaçışlı), iç içe (: :) yorum, EQName ve
/// QName/önek ayrımını doğru çözer; literal/yorum içindeki diziler token üretmez.
/// </summary>
public class XPathTokenizerTests
{
    private static List<XPathToken> T(string e) => XPathTokenizer.Tokenize(e);

    [Fact]
    public void SingleQuotedString_WithEscape_IsOneToken()
    {
        var toks = T("'it''s a doc('");
        Assert.Single(toks);
        Assert.Equal(XPathTokenKind.String, toks[0].Kind);
        Assert.Equal("it's a doc(", toks[0].Text);
    }

    [Fact]
    public void DoubleQuotedString_WithEscape_IsOneToken()
    {
        var toks = T("\"say \"\"hi\"\"\"");
        Assert.Single(toks);
        Assert.Equal("say \"hi\"", toks[0].Text);
    }

    [Fact]
    public void NestedComment_IsStripped()
    {
        // function (: dış (: iç :) :) (
        var toks = T("function (: dış (: iç :) :) (");
        Assert.Equal(2, toks.Count);
        Assert.Equal(XPathTokenKind.Name, toks[0].Kind);
        Assert.Equal("function", toks[0].Text);
        Assert.Equal(XPathTokenKind.LParen, toks[1].Kind);
    }

    [Fact]
    public void EqName_SplitsUriAndLocal()
    {
        var toks = T("Q{http://www.w3.org/2005/xpath-functions}doc('x')");
        Assert.Equal(XPathTokenKind.EqName, toks[0].Kind);
        Assert.Equal("http://www.w3.org/2005/xpath-functions", toks[0].Uri);
        Assert.Equal("doc", toks[0].Text);
        Assert.Equal(XPathTokenKind.LParen, toks[1].Kind);
    }

    [Fact]
    public void Prefix_IsKeptButAxisIsNotConsumedAsPrefix()
    {
        var toks = T("cbc:ID/child::node()");
        Assert.Equal(XPathTokenKind.Name, toks[0].Kind);
        Assert.Equal("cbc:ID", toks[0].Text);
        // child eksen adı; '::' önek ayıracı olarak yutulmaz.
        Assert.Contains(toks, t => t.Kind == XPathTokenKind.Name && t.Text == "child");
    }

    [Fact]
    public void VariableAndHash_AreDistinctTokens()
    {
        var toks = T("$f doc#1");
        Assert.Equal(XPathTokenKind.Var, toks[0].Kind);
        Assert.Equal("f", toks[0].Text);
        Assert.Contains(toks, t => t.Kind == XPathTokenKind.Hash);
        Assert.Contains(toks, t => t.Kind == XPathTokenKind.Number && t.Text == "1");
    }

    [Theory]
    [InlineData("'abc", true)]                 // kapanmamış literal
    [InlineData("1 (: (: :) x", true)]         // kapanmamış iç içe yorum
    [InlineData("Q{urn:x", true)]              // kapanmamış braced URI
    [InlineData("Q{'}f()", true)]              // tırnak içeren braced URI → belirsiz
    [InlineData("Q{a{b}f()", true)]            // '{' içeren braced URI
    [InlineData("concat('a''b', \"c\") (: ok :)", false)]
    [InlineData("Q{urn:x}f()", false)]
    public void Malformed_IsReported(string expr, bool expected)
    {
        XPathTokenizer.Tokenize(expr, out var malformed);
        Assert.Equal(expected, malformed);
    }

    [Theory]
    // (değer, beklenen kapanış '}' indeksi — açılış '{' 0. karakter)
    [InlineData("{a}", 2)]
    [InlineData("{'}'}", 4)]
    [InlineData("{(: } :)a}", 9)]
    [InlineData("{(: (: } :) } :)a}", 17)]
    [InlineData("{map{'a':1}?a}", 13)]
    [InlineData("{Q{urn:x}f()}", 12)]
    public void FindEnclosedExpressionEnd_SkipsLiteralsCommentsAndNestedBraces(string value, int expectedEnd)
    {
        int end = XPathLexer.FindEnclosedExpressionEnd(value, 1, out var malformed);
        Assert.False(malformed);
        Assert.Equal(expectedEnd, end);
    }

    [Theory]
    [InlineData("{a")]
    [InlineData("{(: } a}")]
    [InlineData("{'}")]
    [InlineData("{Q{'}a}")]
    public void FindEnclosedExpressionEnd_Unclosed_IsMalformed(string value)
    {
        XPathLexer.FindEnclosedExpressionEnd(value, 1, out var malformed);
        Assert.True(malformed);
    }

    [Fact]
    public void BraceInsideString_DoesNotBreakTokenizing()
    {
        var toks = T("concat('}', '{')");
        Assert.Equal(XPathTokenKind.Name, toks[0].Kind);
        Assert.Equal("concat", toks[0].Text);
        Assert.Equal(2, toks.Count(t => t.Kind == XPathTokenKind.String));
    }
}
