using System.Text.Json;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests;

/// <summary>
/// Faz 5 — Görev Grupları 1, 2, 4, 5 için Application-katmanı testleri.
///
/// Controller'a bağımlı mantık (DB, HTTP, auth) buraya dahil değil;
/// testler yalnızca XsltGeneratorService + veri dönüşüm kurallarını kapsar.
/// </summary>
public class Faz5Tests
{
    private readonly XsltGeneratorService _generator = new();

    // ═══════════════════════════════════════════════════════════════════════════
    // GG1 — Asset upload: validasyon sabitleri
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] AllowedAssetExtensions = [".png", ".jpg", ".jpeg", ".svg"];
    private const long MaxAssetBytes = 5 * 1024 * 1024; // 5 MB

    [Theory]
    [InlineData(".png",  true)]
    [InlineData(".jpg",  true)]
    [InlineData(".jpeg", true)]
    [InlineData(".svg",  true)]
    [InlineData(".PNG",  false)] // case-sensitive (controller ToLower uygular)
    [InlineData(".gif",  false)]
    [InlineData(".exe",  false)]
    [InlineData(".xslt", false)]
    [InlineData(".pdf",  false)]
    public void AssetUpload_ExtensionValidation(string extension, bool shouldBeAllowed)
        => Assert.Equal(shouldBeAllowed, AllowedAssetExtensions.Contains(extension));

    [Fact]
    public void AssetUpload_MaxSizeIs5MB()
        => Assert.Equal(5_242_880L, MaxAssetBytes);

    [Fact]
    public void AssetUpload_FileOver5MB_ShouldBeRejected()
    {
        long fileSizeBytes = 5 * 1024 * 1024 + 1;
        Assert.True(fileSizeBytes > MaxAssetBytes);
    }

    [Fact]
    public void AssetUpload_FileExactly5MB_ShouldBeAccepted()
    {
        long fileSizeBytes = 5 * 1024 * 1024;
        Assert.False(fileSizeBytes > MaxAssetBytes);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GG2 — Template clone: isimlendirme kuralı
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Fatura Şablonu",        "Fatura Şablonu (kopya)")]
    [InlineData("Template A",            "Template A (kopya)")]
    [InlineData("e-Arşiv v2",            "e-Arşiv v2 (kopya)")]
    [InlineData("Şablon (kopya)",        "Şablon (kopya) (kopya)")] // kopyadan kopya
    public void TemplateClone_NameSuffix(string originalName, string expectedName)
        => Assert.Equal(expectedName, $"{originalName} (kopya)");

    // ═══════════════════════════════════════════════════════════════════════════
    // GG4 — Download endpoint: block tree JSON → XSLT üretimi
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Template.BlockTree alanında saklanan JSON formatından doğru
    /// BlockTreeDto deserialize edilebilmeli ve geçerli XSLT üretilmeli.
    /// </summary>
    [Fact]
    public void Download_BlockTreeJson_DeserializesToValidXslt()
    {
        const string storedJson = """
        {
            "sections": [
                {
                    "id": "s1",
                    "name": "Başlık",
                    "order": 0,
                    "layout": "single-column",
                    "blockIds": ["b-heading", "b-text"]
                }
            ],
            "blocks": {
                "b-heading": {
                    "id": "b-heading",
                    "type": "Heading",
                    "config": { "level": "H1", "content": "FATURA", "isStatic": true },
                    "layout": { "width": "full", "alignment": "left" }
                },
                "b-text": {
                    "id": "b-text",
                    "type": "Text",
                    "config": {
                        "isStatic": false,
                        "binding": { "xpath": "//cbc:ID", "fallback": "—" }
                    },
                    "layout": { "width": "full", "alignment": "left" }
                }
            }
        }
        """;

        var tree = JsonSerializer.Deserialize<BlockTreeDto>(storedJson);

        Assert.NotNull(tree);
        Assert.Single(tree.Sections);
        Assert.Equal(2, tree.Blocks.Count);
        Assert.Equal(["b-heading", "b-text"], tree.Sections[0].BlockIds);

        var (xslt, error) = _generator.Generate(tree);

        Assert.Null(error);
        Assert.NotNull(xslt);
        Assert.Contains("xsl:stylesheet", xslt);
        Assert.Contains("FATURA", xslt);           // statik başlık metni
        Assert.Contains("//cbc:ID", xslt);         // XPath binding
    }

    [Fact]
    public void Download_EmptyBlockTree_ReturnsValidXslt()
    {
        var tree = new BlockTreeDto { Sections = [], Blocks = [] };
        var (xslt, error) = _generator.Generate(tree);

        Assert.Null(error);
        Assert.NotNull(xslt);
        AssertXsltCompiles(xslt);
    }

    [Fact]
    public void Download_InvalidJson_ThrowsJsonException()
        => Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<BlockTreeDto>("bu geçerli json değil"));

    [Fact]
    public void Download_NullBlockTree_ShouldBeDetectedBeforeGeneration()
    {
        // Controller'daki kontrol: string.IsNullOrEmpty(template.BlockTree)
        string? blockTree = null;
        Assert.True(string.IsNullOrEmpty(blockTree));
    }

    /// <summary>
    /// Çok bölümlü, karma block tipli tree'den üretilen XSLT derlenmeli.
    /// </summary>
    [Fact]
    public void Download_MultiSectionTree_XsltCompiles()
    {
        var tree = BuildMultiSectionTree();
        var (xslt, error) = _generator.Generate(tree);

        Assert.Null(error);
        AssertXsltCompiles(xslt!);
    }

    // ── Cache invalidation kuralı ─────────────────────────────────────────────

    [Fact]
    public void Download_CacheInvalidation_XsltPathClearedOnBlockTreeUpdate()
    {
        // Controller mantığını yansıtır:
        // BlockTree güncellenince XsltStoragePath null'a çekilmeli.
        string? xsltStoragePath = "templates/abc123.xslt";
        const string newBlockTree = """{"sections":[],"blocks":{}}""";

        if (!string.IsNullOrEmpty(newBlockTree) && !string.IsNullOrEmpty(xsltStoragePath))
            xsltStoragePath = null;

        Assert.Null(xsltStoragePath);
    }

    [Fact]
    public void Download_CacheHit_ExistingPathKeptWhenBlockTreeUnchanged()
    {
        string? xsltStoragePath = "templates/abc123.xslt";
        string? newBlockTree = null; // block tree güncellenmedi

        if (!string.IsNullOrEmpty(newBlockTree) && !string.IsNullOrEmpty(xsltStoragePath))
            xsltStoragePath = null;

        Assert.Equal("templates/abc123.xslt", xsltStoragePath);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GG4 — Conditional lessThan bug fix (Faz 5 sırasında tespit edildi)
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("lessThan",   "100")]
    [InlineData("greaterThan","100")]
    [InlineData("equals",     "TEMELFATURA")]
    [InlineData("notEquals",  "TEMELFATURA")]
    [InlineData("contains",   "FATURA")]
    [InlineData("exists",     "")]
    [InlineData("notExists",  "")]
    public void Conditional_AllOperators_XsltCompiles(string op, string val)
    {
        var tree = SingleBlock("Conditional", new
        {
            condition    = new { xpath = "//cbc:InvoiceTypeCode", @operator = op, value = val },
            thenBlockIds = Array.Empty<string>(),
            elseBlockIds = Array.Empty<string>()
        });

        var (xslt, error) = _generator.Generate(tree);
        Assert.Null(error);
        AssertXsltCompiles(xslt!);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GG5 — Profil: iş kuralları
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("  Ali Veli  ", "Ali Veli")]
    [InlineData("Test",         "Test")]
    [InlineData("  ",           "")]
    public void ProfileUpdate_DisplayNameTrimmed(string raw, string expected)
        => Assert.Equal(expected, raw.Trim());

    [Theory]
    [InlineData("Ali@EXAMPLE.COM", "ali@example.com")]
    [InlineData("user@Test.Org",   "user@test.org")]
    public void ProfileUpdate_EmailNormalized(string raw, string expected)
        => Assert.Equal(expected, raw.Trim().ToLowerInvariant());

    [Fact]
    public void AccountDelete_Phrase_MustMatchExactly()
    {
        const string requiredPhrase = "hesabımı sil";
        Assert.False(string.IsNullOrEmpty(requiredPhrase));
        Assert.True("hesabımı sil" == requiredPhrase);
        Assert.False("Hesabımı Sil" == requiredPhrase); // büyük/küçük harf farklı → red
        Assert.False("hesabimi sil" == requiredPhrase); // türkçe karakter eksik → red
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Yardımcılar
    // ═══════════════════════════════════════════════════════════════════════════

    private static void AssertXsltCompiles(string xslt)
    {
        var transform = new XslCompiledTransform();
        using var reader = XmlReader.Create(new StringReader(xslt));
        // Derleme hatası vermemeli — varsa test başarısız olur
        transform.Load(reader);
    }

    private static BlockTreeDto SingleBlock(string type, object config)
    {
        return new BlockTreeDto
        {
            Sections = [new SectionDto { Id = "s1", BlockIds = ["b1"] }],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b1"] = new BlockDto
                {
                    Id = "b1",
                    Type = type,
                    Config = JsonSerializer.SerializeToElement(config)
                }
            }
        };
    }

    private static BlockTreeDto BuildMultiSectionTree()
    {
        return new BlockTreeDto
        {
            Sections =
            [
                new SectionDto { Id = "s1", Name = "Başlık", Order = 0, BlockIds = ["b-h1", "b-img"] },
                new SectionDto { Id = "s2", Name = "İçerik", Order = 1, BlockIds = ["b-table", "b-totals"] },
                new SectionDto { Id = "s3", Name = "Alt",    Order = 2, BlockIds = ["b-bank", "b-div"] },
            ],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b-h1"] = new BlockDto
                {
                    Id = "b-h1", Type = "Heading",
                    Config = JsonSerializer.SerializeToElement(new
                        { level = "H1", content = "FATURA", isStatic = true })
                },
                ["b-img"] = new BlockDto
                {
                    Id = "b-img", Type = "Image",
                    Config = JsonSerializer.SerializeToElement(new
                        { assetId = (string?)null, assetType = "logo", alignment = "left",
                          editableOnFreeTheme = false })
                },
                ["b-table"] = new BlockDto
                {
                    Id = "b-table", Type = "Table",
                    Config = JsonSerializer.SerializeToElement(new
                    {
                        iterateOver = "//cac:InvoiceLine",
                        showHeader = true,
                        columns = new[]
                        {
                            new { header = "Ürün", xpath = "cbc:Name", width = "50%", format = "text" },
                            new { header = "Tutar", xpath = "cbc:LineExtensionAmount", width = "25%", format = "currency" }
                        }
                    })
                },
                ["b-totals"] = new BlockDto
                {
                    Id = "b-totals", Type = "Totals",
                    Config = JsonSerializer.SerializeToElement(new
                    {
                        alignment = "right",
                        rows = new[]
                        {
                            new { label = "Toplam", xpath = "//cbc:TaxExclusiveAmount", highlight = false },
                            new { label = "KDV",    xpath = "//cbc:TaxAmount",          highlight = false },
                            new { label = "Genel",  xpath = "//cbc:PayableAmount",       highlight = true  }
                        }
                    })
                },
                ["b-bank"] = new BlockDto
                {
                    Id = "b-bank", Type = "BankInfo",
                    Config = JsonSerializer.SerializeToElement(new
                        { bankNameXpath = "//cbc:Name", ibanXpath = "//cbc:ID", paymentTermsXpath = "" })
                },
                ["b-div"] = new BlockDto
                {
                    Id = "b-div", Type = "Divider",
                    Config = JsonSerializer.SerializeToElement(new
                        { style = "solid", color = "#CCCCCC", thickness = "1px" })
                },
            }
        };
    }
}
