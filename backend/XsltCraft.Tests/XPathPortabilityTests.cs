using System.Text.Json;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests;

/// <summary>
/// <see cref="XPathPortability"/> — grid bağlamaları için çift-motor (XPath 1.0) + maliyet (DoS) kapısı.
/// </summary>
public class XPathPortabilityTests
{
    [Theory]
    [InlineData("cbc:ID", true)]
    [InlineData("//cac:TaxTotal/cbc:TaxAmount", true)]
    [InlineData("n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']", true)]
    [InlineData("cbc:InvoicedQuantity/@unitCode", true)]
    [InlineData(".", true)]
    [InlineData("string(cbc:ID)", false)]
    [InlineData("count(//cbc:ID)", false)]
    [InlineData("concat(cbc:A, ' ', cbc:B)", false)]
    [InlineData("//cbc:ProfileID = 'EARSIVFATURA'", false)]
    [InlineData("cbc:A and (cbc:B)", false)]
    [InlineData("count(//cbc:A) = count(//cbc:B)", false)]
    [InlineData("//cac:Party[.//cbc:ID = '1']", true)]
    [InlineData("//cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode != '0015']", true)]
    [InlineData("//i[@v = ../@w]", true)]
    [InlineData("format-number(number((cbc:A)[1]), '#.##0,00', 'tr')", false)]
    public void Portable(string xpath, bool isNodeSet)
    {
        var r = XPathPortability.Analyze(xpath);
        Assert.True(r.IsPortable, r.Message);
        Assert.Null(r.ErrorCode);
        Assert.Equal(isNodeSet, r.IsNodeSet);
    }

    [Fact]
    public void TooLong_Rejected()
    {
        var r = XPathPortability.Analyze("cbc:" + new string('A', XPathPortability.MaxLength));
        Assert.Equal(XPathPortability.TooLong, r.ErrorCode);
        Assert.False(r.IsPortable);
    }

    [Fact]
    public void DeepParens_RejectedBeforeCompile()
    {
        // Kapanmayan 17 parantez: derlemeye ulaşsaydı XPATH_NOT_PORTABLE (sözdizimi) olurdu —
        // TOO_COMPLEX dönmesi derinlik kapısının derlemeden önce çalıştığını gösterir.
        var unbalanced = XPathPortability.Analyze(new string('(', XPathPortability.MaxParenDepth + 1) + "1");
        Assert.Equal(XPathPortability.TooComplex, unbalanced.ErrorCode);

        // Uzunluk sınırı içinde en derin ifade (255 seviye) — yığın taşırmadan reddedilir.
        var deep = XPathPortability.Analyze(new string('(', 255) + "1" + new string(')', 255));
        Assert.Equal(XPathPortability.TooComplex, deep.ErrorCode);

        var atLimit = XPathPortability.Analyze(new string('(', XPathPortability.MaxParenDepth) + "1" + new string(')', XPathPortability.MaxParenDepth));
        Assert.True(atLimit.IsPortable, atLimit.Message);
    }

    [Fact]
    public void DeepPredicates_Rejected()
    {
        Assert.Equal(XPathPortability.TooComplex, XPathPortability.Analyze("a[b[c[d[e]]]]").ErrorCode);
        Assert.True(XPathPortability.Analyze("a[b[c[d]]]").IsPortable);
    }

    [Theory]
    // Açık #2 (T2.3): 200k düğümde .NET ~9 s / Saxon ~45 s
    [InlineData("count(//i[@v = //i/@w])")]
    [InlineData("//i[@v = //i/@w]")]
    [InlineData("//i[//i/@w = @v]")]
    [InlineData("//i[@v != //i/@w]")]
    [InlineData("//i[@v < //i/@w]")]
    [InlineData("//i[@v <= descendant::i/@w]")]
    [InlineData("//i/@v = //i/@w")]
    [InlineData("//i[.//a = .//b]")]
    [InlineData("//i[@v = following::i/@w]")]
    [InlineData("//i[preceding-sibling::i]")]
    [InlineData("//i[//missing]")]
    [InlineData("//i[j[.//k]]")]
    [InlineData("//i[/descendant::k]")]
    public void QuadraticCost_Rejected(string xpath)
    {
        var r = XPathPortability.Analyze(xpath);
        Assert.False(r.IsPortable);
        Assert.Equal(XPathPortability.TooComplex, r.ErrorCode);
    }

    [Theory]
    [InlineData("upper-case(cbc:ID)")]
    [InlineData("fn:string(cbc:ID)")]
    [InlineData("if (cbc:A) then cbc:B else cbc:C")]
    [InlineData("cbc:ID eq 'x'")]
    [InlineData("(cbc:A, cbc:B)")]
    [InlineData("for $x in cbc:A return $x")]
    [InlineData("document('file:///etc/passwd')")]
    [InlineData("key('k', 'v')")]
    [InlineData("Q{urn:x}a")]
    [InlineData("element(cbc:ID)")]
    [InlineData("'kapanmamış")]
    [InlineData("cbc:ID[")]
    [InlineData("")]
    public void NotPortable_Rejected(string xpath)
    {
        var r = XPathPortability.Analyze(xpath);
        Assert.False(r.IsPortable);
        Assert.Equal(XPathPortability.NotPortable, r.ErrorCode);
    }

    // ── Generator entegrasyonu: fail-closed hata ─────────────────────────

    [Theory]
    [InlineData("count(//i[@v = //i/@w])", "XPATH_TOO_COMPLEX")]
    [InlineData("upper-case(//cbc:ID)", "XPATH_NOT_PORTABLE")]
    public void Generator_NonPortableBinding_FailsClosed(string xpath, string code)
    {
        var tree = new
        {
            version = 2,
            documentType = "Invoice",
            blocks = new Dictionary<string, object>
            {
                ["t1"] = new
                {
                    id = "t1",
                    type = "Text",
                    config = new { isStatic = false, binding = new { xpath } },
                    gridLayout = new { x = 10, y = 10, width = 100, height = 8, autoHeight = true },
                },
            },
        };

        var (xslt, error) = new XsltGeneratorService().GenerateFromJson(JsonSerializer.Serialize(tree));

        Assert.Null(xslt);
        Assert.NotNull(error);
        Assert.StartsWith($"{code}: '{xpath}'", error);
        Assert.Contains("Text bloğunda (t1)", error);
    }
}
