using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Xsl;
using VerifyXunit;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests.Eval;

/// <summary>
/// XSLT generator eval harness. Case'ler temsili block-tree'lerden XSLT üretir ve
/// "korunması gereken" değişmezleri (namespace / version / loop & binding / güvenlik)
/// doğrular. Regresyon ağı niteliğindedir; insan-okur tanımlar: docs/ecc/evaluations/.
///
/// Lokal: dotnet test --filter "Category=Eval"
/// CI'da non-blocking ayrı job'ta koşar (bkz. .github/workflows/ci.yml).
/// </summary>
[Trait("Category", "Eval")]
public class XsltEvalTests
{
    private readonly XsltGeneratorService _sut = new();

    // Üretilen stylesheet'te korunması gereken UBL-TR namespace URI'leri.
    private static readonly string[] RequiredNamespaceUris =
    [
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
        "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2",
    ];

    private const string SupplierNameXPath =
        "//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name";
    private const string InvoiceLineXPath = "//cac:InvoiceLine";

    // ── case001 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Case001_PreservesInvariants()
    {
        var (xslt, error) = _sut.Generate(BuildCase001Tree());

        Assert.Null(error);
        Assert.NotNull(xslt);

        // 1) Sertleştirilmiş reader ile derlenebilir olmalı (XXE guard pattern'i in action).
        AssertCompilesHardened(xslt!);

        // 2) version korunur.
        Assert.Contains("version=\"2.0\"", xslt);

        // 3) Namespace bütünlüğü.
        foreach (var uri in RequiredNamespaceUris)
            Assert.Contains(uri, xslt);
        Assert.Contains("exclude-result-prefixes=\"n1 cbc cac ext\"", xslt);

        // 4) Loop bütünlüğü — InvoiceLine iterasyonu kaldırılamaz.
        Assert.Contains("<xsl:for-each", xslt);
        Assert.Contains(InvoiceLineXPath, xslt);

        // 5) Binding bütünlüğü — bound alanlar value-of olarak görünür.
        Assert.Contains("<xsl:value-of", xslt);
        Assert.Contains(SupplierNameXPath, xslt);

        // 6) Güvenlik — script/extension enjeksiyonu yok.
        Assert.DoesNotContain("msxsl:script", xslt);
        Assert.DoesNotContain("xmlns:msxsl", xslt);
        Assert.DoesNotContain("<script", xslt);
    }

    [Fact]
    public Task Case001_StructuralSnapshot()
    {
        var (xslt, error) = _sut.Generate(BuildCase001Tree());
        Assert.Null(error);
        Assert.NotNull(xslt);

        // CSS gürültüsüne dayanıklı yapısal projeksiyon: yalnız XSLT yapısı + namespace'ler.
        var projection = StructuralProjection(xslt!);
        return Verifier.Verify(projection).UseDirectory("__snapshots__");
    }

    // ── case002: SSRF / harici kaynak fonksiyonları reddedilir ───────────────

    [Theory]
    [InlineData("document('http://169.254.169.254/latest/meta-data/')")]
    [InlineData("doc('http://attacker.example/x')")]
    [InlineData("unparsed-text('file:///etc/passwd')")]
    [InlineData("collection('file:///')")]
    public void Case002_RejectsExternalResourceFunctions(string maliciousXpath)
    {
        // Kullanıcı binding XPath'i üretilen XSLT'ye gömülür; Saxon render yolunda bu
        // fonksiyonlar SSRF/dosya okuma sağlar. Generator fail-closed reddetmeli.
        var tree = SingleTextBinding(maliciousXpath);

        var (xslt, error) = _sut.Generate(tree);

        Assert.Null(xslt);
        Assert.NotNull(error);
    }

    [Fact]
    public void Case002_AllowsBenignXPath()
    {
        // Regresyon: meşru bir UBL XPath'i engellenmemeli.
        var (xslt, error) = _sut.Generate(SingleTextBinding("//cbc:Note"));

        Assert.Null(error);
        Assert.NotNull(xslt);
        Assert.Contains("//cbc:Note", xslt);
    }

    private static BlockTreeDto SingleTextBinding(string xpath) => new()
    {
        Sections = [new SectionDto { Id = "s1", Name = "T", Order = 1, BlockIds = ["b1"] }],
        Blocks = new Dictionary<string, BlockDto>
        {
            ["b1"] = Block("b1", "Text", new { isStatic = false, binding = new { xpath } }),
        },
    };

    // ── yardımcılar ──────────────────────────────────────────────────────────

    /// <summary>
    /// Çıktıdan yalnız yapısal anlam taşıyan satırları (xsl:* talimatları + namespace
    /// bildirimleri) süzer. CSS / statik HTML hariç tutulur → snapshot düşük gürültülü.
    /// </summary>
    private static string StructuralProjection(string xslt)
    {
        var lines = xslt.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.Contains("xsl:") || line.Contains("xmlns:") || line.Contains("exclude-result-prefixes"))
                sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Üretilen XSLT'yi sertleştirilmiş ayarlarla derler; Load hata fırlatmazsa geçerlidir.
    /// constraints.md'deki guard pattern'inin canlı uygulamasıdır.
    /// </summary>
    private static void AssertCompilesHardened(string xslt)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        var transform = new XslCompiledTransform();
        using var reader = XmlReader.Create(new StringReader(xslt), settings);
        transform.Load(reader);
    }

    private static BlockTreeDto BuildCase001Tree()
    {
        return new BlockTreeDto
        {
            Sections =
            [
                new SectionDto
                {
                    Id = "s1",
                    Name = "Fatura",
                    Order = 1,
                    BlockIds = ["heading", "docinfo", "supplier", "lines", "totals"],
                },
            ],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["heading"] = Block("heading", "Heading",
                    new { level = "H1", isStatic = true, content = "FATURA" }),

                ["docinfo"] = Block("docinfo", "DocumentInfo", new
                {
                    rows = new object[]
                    {
                        new { label = "Fatura No", xpath = "//cbc:ID" },
                        new { label = "Fatura Tarihi", xpath = "//cbc:IssueDate" },
                    },
                }),

                ["supplier"] = Block("supplier", "Text", new
                {
                    isStatic = false,
                    binding = new { xpath = SupplierNameXPath, fallback = "—" },
                }),

                ["lines"] = Block("lines", "Table", new
                {
                    iterateOver = InvoiceLineXPath,
                    showHeader = true,
                    headerBackgroundColor = "#E0E0E0",
                    columns = new object[]
                    {
                        new { header = "No", xpath = "cbc:ID", width = "8%" },
                        new { header = "Ürün", xpath = "cac:Item/cbc:Name", width = "52%" },
                        new { header = "Miktar", xpath = "cbc:InvoicedQuantity", width = "20%" },
                        new { header = "Tutar", xpath = "cbc:LineExtensionAmount", width = "20%" },
                    },
                }),

                ["totals"] = Block("totals", "Totals", new
                {
                    alignment = "right",
                    rows = new object[]
                    {
                        new
                        {
                            label = "Ödenecek Tutar",
                            xpath = "//cac:LegalMonetaryTotal/cbc:PayableAmount",
                            highlight = true,
                        },
                    },
                }),
            },
        };
    }

    private static BlockDto Block(string id, string type, object config) =>
        new() { Id = id, Type = type, Config = JsonSerializer.SerializeToElement(config) };
}
