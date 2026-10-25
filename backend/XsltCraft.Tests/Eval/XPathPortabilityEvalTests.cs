using System.Text.Json;
using System.Xml;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Tests.Eval;

/// <summary>
/// Eval case011 — grid'de taşınamaz/pahalı XPath (G3 kapısı) yalnız kendi bloğunu yumuşak düşürür:
/// yer tutucu + <see cref="XsltGenerationWarning"/>; şablonun geri kalanı üretilir ve render edilir.
/// İnsan-okur: docs/ecc/evaluations/xslt/case011-xpath-portability.md. Birim kapsamı: <c>XPathPortabilityTests</c>.
/// </summary>
[Trait("Category", "Eval")]
public class XPathPortabilityEvalTests
{
    private const string AncestorScan = "//*[..//zz]";
    private const string QuadraticJoin = "count(//i[@v = //i/@w])";

    private const string Invoice =
        """
        <n1:Invoice xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                    xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                    xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:ID>FAT2026000011</cbc:ID>
          <cac:AccountingSupplierParty>
            <cac:Party><cac:PartyName><cbc:Name>ACME Ltd</cbc:Name></cac:PartyName></cac:Party>
          </cac:AccountingSupplierParty>
        </n1:Invoice>
        """;

    [Fact]
    public void Case011_ComplexXPath_SoftFailsBlocks_RestOfTemplateRendered()
    {
        var generator = new XsltGeneratorService();
        var (xslt, error) = generator.GenerateFromJson(Tree(
            TextBlock("number", 10, "//cbc:ID"),
            TextBlock("ancestorScan", 20, AncestorScan),
            TextBlock("quadratic", 30, QuadraticJoin),
            TextBlock("supplier", 40, "//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name")));

        // Şablon reddedilmez; yalnız iki blok uyarı üretir.
        Assert.Null(error);
        Assert.NotNull(xslt);
        Assert.Equal(
            [
                new XsltGenerationWarning("ancestorScan", XPathPortability.TooComplex, AncestorScan),
                new XsltGenerationWarning("quadratic", XPathPortability.TooComplex, QuadraticJoin),
            ],
            generator.Warnings.OrderBy(w => w.BlockId, StringComparer.Ordinal));

        // Stylesheet değişmezleri korunur; kötü ifadeler stylesheet'e girmez.
        Assert.Contains("version=\"2.0\"", xslt);
        Assert.Contains("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", xslt);
        Assert.DoesNotContain(AncestorScan, xslt);
        Assert.DoesNotContain(QuadraticJoin, xslt);
        AssertParsesHardened(xslt!);

        // Kötü bloklar yer tutucu; iyi bloklar render edilir.
        var html = new XsltHtmlRenderer().Render(xslt, Invoice).Html;
        Assert.Contains("xc-binding-error", html);
        Assert.Contains($"Bu blok desteklenmeyen bir XPath içeriyor ({XPathPortability.TooComplex})", html);
        Assert.Contains("FAT2026000011", html);
        Assert.Contains("ACME Ltd", html);
    }

    private static object TextBlock(string id, int y, string xpath) => new
    {
        id,
        type = "Text",
        config = new { isStatic = false, binding = new { xpath } },
        gridLayout = new { x = 10, y, width = 100, height = 8, autoHeight = true },
    };

    private static string Tree(params object[] blocks) => JsonSerializer.Serialize(new
    {
        version = 2,
        documentType = "Invoice",
        blocks = blocks.ToDictionary(b => (string)b.GetType().GetProperty("id")!.GetValue(b)!),
    });

    private static void AssertParsesHardened(string xslt)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xslt), settings);
        while (reader.Read()) { }
    }
}
