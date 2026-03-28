using System.Text.Json;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests;

/// <summary>
/// Her block tipi için üretilen XSLT'nin XslCompiledTransform ile compile edilebilir
/// (yani valid XSLT 1.0) olduğunu doğrular.
/// </summary>
public class XsltGeneratorTests
{
    private readonly XsltGeneratorService _sut = new();

    // ── yardımcı: tek bloklu tree oluştur ────────────────────────────────────

    private static BlockTreeDto SingleBlock(string type, object config)
    {
        var configJson = JsonSerializer.SerializeToElement(config);
        return new BlockTreeDto
        {
            Sections =
            [
                new SectionDto { Id = "s1", Name = "Test", Order = 1, BlockIds = ["b1"] }
            ],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b1"] = new BlockDto { Id = "b1", Type = type, Config = configJson }
            }
        };
    }

    private void AssertValid(BlockTreeDto tree)
    {
        var (xslt, error) = _sut.Generate(tree);
        Assert.Null(error);
        Assert.NotNull(xslt);

        // Bağımsız XmlReader ile tekrar doğrula
        var transform = new XslCompiledTransform();
        using var reader = XmlReader.Create(new StringReader(xslt!));
        // Load hata fırlatmazsa XSLT geçerlidir
        transform.Load(reader);
    }

    // ── BLOCK-01: Text ────────────────────────────────────────────────────

    [Fact]
    public void Text_Static_GeneratesValidXslt()
        => AssertValid(SingleBlock("Text", new { isStatic = true, content = "Merhaba" }));

    [Fact]
    public void Text_Bound_GeneratesValidXslt()
        => AssertValid(SingleBlock("Text", new
        {
            isStatic = false,
            binding = new { xpath = "//cbc:Name", fallback = "—" }
        }));

    // ── BLOCK-02: Heading ─────────────────────────────────────────────────

    [Fact]
    public void Heading_Static_GeneratesValidXslt()
        => AssertValid(SingleBlock("Heading", new { level = "H2", isStatic = true, content = "Başlık" }));

    [Fact]
    public void Heading_Bound_GeneratesValidXslt()
        => AssertValid(SingleBlock("Heading", new
        {
            level = "H1",
            isStatic = false,
            binding = new { xpath = "//cbc:Name" }
        }));

    // ── BLOCK-03: Paragraph ───────────────────────────────────────────────

    [Fact]
    public void Paragraph_Mixed_GeneratesValidXslt()
        => AssertValid(SingleBlock("Paragraph", new
        {
            lines = new object[]
            {
                new { isStatic = true,  content = "Tel: " },
                new { isStatic = false, xpath = "//cbc:Telephone" }
            }
        }));

    // ── BLOCK-04: Table ───────────────────────────────────────────────────

    [Fact]
    public void Table_GeneratesValidXslt()
        => AssertValid(SingleBlock("Table", new
        {
            iterateOver = "//cac:InvoiceLine",
            showHeader = true,
            headerBackgroundColor = "#E0E0E0",
            columns = new object[]
            {
                new { header = "No",  xpath = "cbc:ID",   width = "5%"  },
                new { header = "Ad",  xpath = "cac:Item/cbc:Name", width = "30%" }
            }
        }));

    // ── BLOCK-05: ForEach ─────────────────────────────────────────────────

    [Fact]
    public void ForEach_WithChildren_GeneratesValidXslt()
    {
        var tree = new BlockTreeDto
        {
            Sections =
            [
                new SectionDto { Id = "s1", Name = "Test", Order = 1, BlockIds = ["b1"] }
            ],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b1"] = new BlockDto
                {
                    Id = "b1",
                    Type = "ForEach",
                    Config = JsonSerializer.SerializeToElement(new
                    {
                        iterateOver = "//cac:InvoiceLine",
                        children = new[] { "b2" }
                    })
                },
                ["b2"] = new BlockDto
                {
                    Id = "b2",
                    Type = "Text",
                    Config = JsonSerializer.SerializeToElement(new { isStatic = true, content = "satır" })
                }
            }
        };
        AssertValid(tree);
    }

    // ── BLOCK-06: Conditional ─────────────────────────────────────────────

    [Theory]
    [InlineData("equals", "TEMELFATURA")]
    [InlineData("notEquals", "TEMELFATURA")]
    [InlineData("contains", "FATURA")]
    [InlineData("greaterThan", "100")]
    [InlineData("lessThan", "100")]
    [InlineData("exists", "")]
    [InlineData("notExists", "")]
    public void Conditional_AllOperators_GenerateValidXslt(string op, string val)
        => AssertValid(SingleBlock("Conditional", new
        {
            condition = new { xpath = "//cbc:InvoiceTypeCode", @operator = op, value = val },
            thenBlockIds = Array.Empty<string>(),
            elseBlockIds = Array.Empty<string>()
        }));

    [Fact]
    public void Conditional_WithElse_GeneratesValidXslt()
        => AssertValid(SingleBlock("Conditional", new
        {
            condition = new { xpath = "//cbc:InvoiceTypeCode", @operator = "equals", value = "TEMELFATURA" },
            thenBlockIds = Array.Empty<string>(),
            elseBlockIds = new[] { "nonexistent" }
        }));

    // ── BLOCK-07: Image ───────────────────────────────────────────────────

    [Fact]
    public void Image_GeneratesValidXslt()
        => AssertValid(SingleBlock("Image", new
        {
            assetType = "logo",
            alignment = "center",
            width = "150px",
            height = "80px",
            altText = "Logo",
            editableOnFreeTheme = true
        }));

    // ── BLOCK-08: DocumentInfo ────────────────────────────────────────────

    [Fact]
    public void DocumentInfo_GeneratesValidXslt()
        => AssertValid(SingleBlock("DocumentInfo", new
        {
            rows = new object[]
            {
                new { label = "Fatura No",    xpath = "//cbc:ID" },
                new { label = "Fatura Tarihi", xpath = "//cbc:IssueDate" }
            }
        }));

    // ── BLOCK-09: Totals ──────────────────────────────────────────────────

    [Fact]
    public void Totals_GeneratesValidXslt()
        => AssertValid(SingleBlock("Totals", new
        {
            alignment = "right",
            rows = new object[]
            {
                new { label = "Toplam", xpath = "//cac:LegalMonetaryTotal/cbc:PayableAmount", highlight = true }
            }
        }));

    // ── BLOCK-10: Notes ───────────────────────────────────────────────────

    [Fact]
    public void Notes_GeneratesValidXslt()
        => AssertValid(SingleBlock("Notes", new
        {
            iterateOver = "//cbc:Note",
            prefix = "Not: "
        }));

    // ── BLOCK-11: BankInfo ────────────────────────────────────────────────

    [Fact]
    public void BankInfo_GeneratesValidXslt()
        => AssertValid(SingleBlock("BankInfo", new
        {
            bankNameXpath = "//cac:PaymentMeans/cac:PayeeFinancialAccount/cac:FinancialInstitutionBranch/cac:FinancialInstitution/cbc:Name",
            ibanXpath = "//cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:ID",
            paymentTermsXpath = "//cac:PaymentTerms/cbc:Note"
        }));

    // ── BLOCK-12: ETTN ────────────────────────────────────────────────────

    [Fact]
    public void ETTN_WithQR_GeneratesValidXslt()
        => AssertValid(SingleBlock("ETTN", new { ettnXpath = "//cbc:UUID", showQR = true }));

    [Fact]
    public void ETTN_WithoutQR_GeneratesValidXslt()
        => AssertValid(SingleBlock("ETTN", new { ettnXpath = "//cbc:UUID", showQR = false }));

    // ── BLOCK-13: Divider ─────────────────────────────────────────────────

    [Theory]
    [InlineData("solid")]
    [InlineData("dashed")]
    [InlineData("dotted")]
    public void Divider_AllStyles_GenerateValidXslt(string style)
        => AssertValid(SingleBlock("Divider", new
        {
            style,
            color = "#CCCCCC",
            thickness = "1px",
            marginTop = "8px",
            marginBottom = "8px"
        }));

    // ── BLOCK-14: Spacer ──────────────────────────────────────────────────

    [Fact]
    public void Spacer_GeneratesValidXslt()
        => AssertValid(SingleBlock("Spacer", new { height = "24px" }));

    // ── Boş tree ─────────────────────────────────────────────────────────

    [Fact]
    public void EmptyTree_GeneratesValidXslt()
        => AssertValid(new BlockTreeDto());

    // ── GenerateFromJson ──────────────────────────────────────────────────

    [Fact]
    public void GenerateFromJson_ValidJson_ReturnsXslt()
    {
        var json = JsonSerializer.Serialize(SingleBlock("Spacer", new { height = "16px" }));
        var (xslt, error) = _sut.GenerateFromJson(json);
        Assert.Null(error);
        Assert.NotNull(xslt);
    }

    [Fact]
    public void GenerateFromJson_InvalidJson_ReturnsError()
    {
        var (xslt, error) = _sut.GenerateFromJson("NOT JSON");
        Assert.Null(xslt);
        Assert.NotNull(error);
    }
}
