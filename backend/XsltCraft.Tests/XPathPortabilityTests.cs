using System.Text.Json;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;

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
    // G3: önceki meşru desenler + yerel (komşu) kardeş + sınırlı yukarı eksen
    [InlineData("cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']", true)]
    [InlineData("//cbc:Note[1]", true)]
    [InlineData("cac:InvoiceLine[cbc:ID='1']", true)]
    [InlineData("/*[local-name()='Invoice']/cac:InvoiceLine[3]/cac:Item/cbc:Name", true)]
    [InlineData("/*[local-name()='Invoice']/ext:UBLExtensions/ext:UBLExtension[2]/ext:ExtensionContent/*[local-name()='Signature']/*[local-name()='SignedInfo']/*[local-name()='Reference'][1]/*[local-name()='DigestValue']", true)]
    [InlineData("cac:TaxSubtotal[not(preceding-sibling::*[1]/cbc:Percent[1] = cbc:Percent[1])]", true)]
    [InlineData("cac:InvoiceLine[following-sibling::node()[1]]", true)]
    [InlineData("//*[not(..)]", true)]
    [InlineData("//*[count(ancestor::*) > 3]", true)]
    [InlineData("//cbc:ID[name(..) = 'cac:Party']", true)]
    [InlineData("//cbc:ID[../../@schemeID = 'x']", true)]
    [InlineData("//cbc:A | //cbc:B | //cbc:C | //cbc:D", true)]
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
    // G3 security probe vektörleri (yüklemde yukarı eksen + //, ata dize değeri, sınırsız birleşim)
    [InlineData("//*[ancestor::*//zz]")]
    [InlineData("//*[..//zz]")]
    [InlineData("//*[../..//zz]")]
    [InlineData("//*[ancestor::node()//zz]")]
    [InlineData("//*[ancestor::*/descendant::zz]")]
    [InlineData("//*[ancestor-or-self::*//zz]")]
    [InlineData("count(//*[ancestor::*//zz])")]
    [InlineData("//*[count(ancestor::*//*) = 0]")]
    [InlineData("//*[contains(ancestor::*, 'zz')]")]
    [InlineData("//*[string(ancestor::*)='q']")]
    [InlineData("//*[. = ancestor::*]")]
    [InlineData("//*[sum(ancestor::*) > 0]")]
    [InlineData("sum(//*[ancestor::*[1]//zz])")]
    [InlineData("//a | //b | //c | //d | //e")]
    // G3 ölçümüyle daraltılan: kardeş taraması, geniş ebeveynin çocukları, iki geniş küme
    [InlineData("cac:TaxSubtotal[not(preceding-sibling::cac:TaxSubtotal/cbc:Percent = cbc:Percent)]")]
    [InlineData("//*[preceding-sibling::zz]")]
    [InlineData("a[b[preceding-sibling::*[1]]]")]
    [InlineData("//*[../zz]")]
    [InlineData("//*[ancestor::*/zz]")]
    [InlineData("//*[string(..) = 'q']")]
    [InlineData("//*[current()//zz]")]
    [InlineData("//*[$v/descendant::zz]")]
    [InlineData("r[s/p = s/q]")]
    [InlineData("s/p != s/p")]
    [InlineData("a[b][c][d][e][f]")]
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

    // ── Generator entegrasyonu: blok düzeyinde yumuşak düşüş (G3) ───────

    private const string SampleXml =
        "<Invoice xmlns='urn:oasis:names:specification:ubl:schema:xsd:Invoice-2' " +
        "xmlns:cbc='urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2'>" +
        "<cbc:ID>INV-0001</cbc:ID></Invoice>";

    private static object TextBlock(string id, int y, string xpath) => new
    {
        id,
        type = "Text",
        config = new { isStatic = false, binding = new { xpath } },
        gridLayout = new { x = 10, y, width = 100, height = 8, autoHeight = true },
    };

    private static string Tree(Dictionary<string, object> blocks) =>
        JsonSerializer.Serialize(new { version = 2, documentType = "Invoice", blocks });

    [Theory]
    [InlineData("count(//i[@v = //i/@w])", "XPATH_TOO_COMPLEX")]
    [InlineData("upper-case(//cbc:ID)", "XPATH_NOT_PORTABLE")]
    [InlineData("//*[ancestor::*//zz]", "XPATH_TOO_COMPLEX")]
    public void UnportableBinding_SoftFails_BlockOnly(string xpath, string code)
    {
        var generator = new XsltGeneratorService();
        var (xslt, error) = generator.GenerateFromJson(Tree(new Dictionary<string, object>
        {
            ["bad"] = TextBlock("bad", 10, xpath),
            ["good"] = TextBlock("good", 30, "//cbc:ID"),
        }));

        Assert.Null(error);
        Assert.NotNull(xslt);
        var warning = Assert.Single(generator.Warnings);
        Assert.Equal(new XsltGenerationWarning("bad", code, xpath), warning);

        var html = new XsltHtmlRenderer().Render(xslt, SampleXml).Html;
        Assert.Contains("xc-binding-error", html);
        Assert.Contains($"Bu blok desteklenmeyen bir XPath içeriyor ({code})", html);
        Assert.Contains("INV-0001", html); // diğer blok render edildi
        Assert.DoesNotContain(xpath, xslt); // kötü ifade stylesheet'e girmez
    }

    [Fact]
    public void UnportableBinding_ConditionalText_SoftFails()
    {
        const string badThen = "//*[contains(ancestor::*, 'zz')]";
        var generator = new XsltGeneratorService();
        var (xslt, error) = generator.GenerateFromJson(Tree(new Dictionary<string, object>
        {
            ["cond"] = new
            {
                id = "cond",
                type = "ConditionalText",
                config = new
                {
                    condition = new { xpath = "//cbc:ID", @operator = "exists" },
                    thenIsStatic = false,
                    thenContent = badThen,
                    elseIsStatic = true,
                    elseContent = "yok",
                },
                gridLayout = new { x = 10, y = 10, width = 100, height = 8, autoHeight = true },
            },
            ["good"] = TextBlock("good", 30, "//cbc:ID"),
        }));

        Assert.Null(error);
        var warning = Assert.Single(generator.Warnings);
        Assert.Equal(("cond", XPathPortability.TooComplex, badThen), (warning.BlockId, warning.Code, warning.XPath));

        var html = new XsltHtmlRenderer().Render(xslt!, SampleXml).Html;
        Assert.Contains("xc-binding-error", html);
        Assert.Contains("INV-0001", html);

        // Uyarılar çağrı başınadır: temiz ağaçta boşalır.
        generator.GenerateFromJson(Tree(new Dictionary<string, object> { ["good"] = TextBlock("good", 10, "//cbc:ID") }));
        Assert.Empty(generator.Warnings);
    }
}
