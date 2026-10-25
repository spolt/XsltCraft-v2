namespace XsltCraft.Tests.Support;

public class HtmlNormalizerTests
{
    private const string Body = "<html><head><meta charset=\"UTF-8\"><title>e-Belge</title></head><body><p>Merhaba</p></body></html>";

    [Theory]
    [InlineData("")]
    [InlineData("<!DOCTYPE html>")]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\" \"http://www.w3.org/TR/html4/loose.dtd\">")]
    [InlineData("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">")]
    public void Normalize_DoctypeVariants_ProduceIdenticalOutput(string doctype)
    {
        Assert.Equal(HtmlNormalizer.Normalize(Body), HtmlNormalizer.Normalize(doctype + Body));
    }

    [Theory]
    [InlineData("<META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">")]
    [InlineData("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">")]
    [InlineData("<META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-16\">")]
    public void Normalize_MetaHttpEquiv_IsDroppedAndMetaCharsetKept(string httpEquivMeta)
    {
        var withMeta = Body.Replace("<head>", "<head>" + httpEquivMeta);

        var normalized = HtmlNormalizer.Normalize(withMeta);

        Assert.Equal(HtmlNormalizer.Normalize(Body), normalized);
        Assert.DoesNotContain("http-equiv", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<meta charset=\"UTF-8\">", normalized);
    }

    [Fact]
    public void Normalize_SaxonReplacesMetaCharsetWithHttpEquiv_EqualsMetaCharset()
    {
        // Saxon html çıktı yöntemi şablonun <meta charset>'ını kaldırıp yerine Content-Type http-equiv yazar.
        var saxon = Body.Replace("<meta charset=\"UTF-8\">",
            "<meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">");

        Assert.Equal(HtmlNormalizer.Normalize(Body), HtmlNormalizer.Normalize(saxon));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<meta charset=\"ISO-8859-9\">")]
    [InlineData("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1254\">")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"5\">")]
    public void Normalize_MissingOrDifferentCharset_RemainsVisible(string meta)
    {
        var other = Body.Replace("<meta charset=\"UTF-8\">", meta);

        Assert.NotEqual(HtmlNormalizer.Normalize(Body), HtmlNormalizer.Normalize(other));
    }

    [Fact]
    public void Normalize_IndentationWhitespace_IsIgnored()
    {
        const string compact = "<html><body><div><table><tr><td>A  B</td></tr></table></div></body></html>";
        const string indented = """
            <html>
              <body>
                <div>
                  <table>
                    <tr>
                      <td>
                        A
                        B
                      </td>
                    </tr>
                  </table>
                </div>
              </body>
            </html>
            """;

        Assert.Equal(HtmlNormalizer.Normalize(compact), HtmlNormalizer.Normalize(indented));
    }

    [Theory]
    [InlineData("<p>1&nbsp;Adet &amp; 2</p>")]
    [InlineData("<p>1&#160;Adet &#38; 2</p>")]
    [InlineData("<p>1&#xA0;Adet &#x26; 2</p>")]
    [InlineData("<p>1 Adet &amp; 2</p>")]
    public void Normalize_EquivalentEntities_ProduceCanonicalForm(string paragraph)
    {
        var normalized = HtmlNormalizer.Normalize($"<html><body>{paragraph}</body></html>");

        Assert.Contains("<p>1&nbsp;Adet &amp; 2</p>", normalized);
    }

    [Fact]
    public void Normalize_NonBreakingSpace_IsNotCollapsedIntoRegularSpace()
    {
        var nbsp = HtmlNormalizer.Normalize("<html><body><p>a&nbsp;b</p></body></html>");
        var space = HtmlNormalizer.Normalize("<html><body><p>a b</p></body></html>");

        Assert.NotEqual(nbsp, space);
    }

    [Fact]
    public void Normalize_AttributeOrder_IsCanonical()
    {
        var a = HtmlNormalizer.Normalize("<html><body><td style=\"width:50%\" class=\"lbl\" colspan=\"2\">x</td></body></html>");
        var b = HtmlNormalizer.Normalize("<html><body><td colspan=\"2\" class=\"lbl\" style=\"width:50%\">x</td></body></html>");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Normalize_AttributeValueWithQuoteAndAmpersand_IsEscaped()
    {
        var normalized = HtmlNormalizer.Normalize("<html><body><img alt='A &amp; \"B\"'></body></html>");

        Assert.Contains("<img alt=\"A &amp; &quot;B&quot;\">", normalized);
    }

    [Fact]
    public void Normalize_Comments_AreDropped()
    {
        var withComment = HtmlNormalizer.Normalize("<html><body><!-- not --><p>x</p><!-- son --></body></html>");
        var without = HtmlNormalizer.Normalize("<html><body><p>x</p></body></html>");

        Assert.Equal(without, withComment);
    }

    [Fact]
    public void Normalize_TextDifference_IsPreserved()
    {
        var first = HtmlNormalizer.Normalize("<html><body><td>1.234,50 TL</td></body></html>");
        var second = HtmlNormalizer.Normalize("<html><body><td>1.234,51 TL</td></body></html>");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Normalize_ScriptAndStyleContent_KeptRawWithCollapsedWhitespace()
    {
        const string html = """
            <html><head><style>
              .a td { padding:2px; }
            </style></head><body><script>
              if (a < b && c) {
                run();
              }
            </script></body></html>
            """;

        var normalized = HtmlNormalizer.Normalize(html);

        Assert.Contains("<style>.a td { padding:2px; }</style>", normalized);
        Assert.Contains("<script>if (a < b && c) { run(); }</script>", normalized);
    }

    [Fact]
    public void Normalize_MixedContent_WritesEachChildOnOwnIndentedLine()
    {
        var normalized = HtmlNormalizer.Normalize("<html><body><p><strong>Not: </strong>Metin</p></body></html>");

        Assert.Equal(
            """
            <html>
              <head></head>
              <body>
                <p>
                  <strong>Not:</strong>
                  Metin
                </p>
              </body>
            </html>

            """.ReplaceLineEndings("\n"),
            normalized);
    }
}
