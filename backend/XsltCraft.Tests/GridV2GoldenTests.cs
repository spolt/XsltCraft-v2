using System.Text.Json;
using VerifyXunit;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;
using XsltCraft.Tests.Support;

namespace XsltCraft.Tests;

/// <summary>
/// Grid V2 generator'ının (XsltGeneratorService.GenerateV2) mevcut davranışının golden taban çizgisi.
///
/// Her fixture için (a) üretilen XSLT metni, her (fixture × uygun XML) için (b) önizlemenin
/// bugünkü render yolunun (PreviewController.Preview → IXsltHtmlRenderer → XsltHtmlRenderer,
/// .NET XslCompiledTransform) normalize edilmiş HTML çıktısı snapshot'lanır.
///
/// Sonraki dalgada generator "çift-motor güvenli" hale getirilip (tekil değer sarmalama,
/// format-number(number(...))) önizleme Saxon'a geçirildiğinde, davranış farkları bu
/// snapshot'larda bilinçli güncelleme olarak görünür. XSLT XML'den bağımsız olduğu için
/// fixture başına tek XSLT snapshot'ı alınır.
///
/// Fixture'lar: Fixtures/GridV2/*.json · XML'ler: Fixtures/Xml/*.xml · Snapshot'lar: __snapshots__/GridV2/
/// </summary>
public class GridV2GoldenTests
{
    private const string SnapshotDir = "__snapshots__/GridV2";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Önizleme denetleyicisinin storage'dan çözdüğü asset → data URI eşlemesinin deterministik karşılığı.
    /// layout-scenarios'taki ikinci asset kasıtlı olarak eşlenmez (/api/assets/{id}/serve yolu).
    /// </summary>
    private static readonly Dictionary<string, string> Assets = new()
    {
        ["aaaaaaaa-0000-4000-8000-000000000001"] =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
    };

    /// <summary>frontend/xsltcraft-ui/src/types/blocks.ts → <c>BlockType</c> birleşimi.</summary>
    private static readonly string[] FrontendBlockTypes =
    [
        "Text", "Heading", "Paragraph", "Table", "Image", "DocumentInfo", "Totals", "Notes", "BankInfo",
        "ETTN", "Divider", "Spacer", "ConditionalText", "TaxSummary", "GibKarekod", "PartyInfo",
        "InvoiceLineTable", "InvoiceHeader", "InvoiceTotals", "GibLogo", "ShipmentInfo",
    ];

    private static readonly string[] InvoiceXmls =
        ["invoice-basic", "invoice-multivalue", "invoice-empty-numeric", "invoice-rounding"];

    private static readonly string[] FixtureNames =
        ["invoice-content-blocks", "invoice-ready-blocks", "layout-scenarios", "despatch-ready-blocks"];

    public static TheoryData<string> Fixtures => new(FixtureNames);

    public static TheoryData<string, string> RenderCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var xml in InvoiceXmls)
            {
                data.Add("invoice-content-blocks", xml);
                data.Add("invoice-ready-blocks", xml);
            }
            // Yerleşim senaryosu XML'e duyarsızdır; tek bir temel faturayla yeterli.
            data.Add("layout-scenarios", "invoice-basic");
            data.Add("despatch-ready-blocks", "despatch-basic");
            return data;
        }
    }

    // ── (a) XSLT snapshot ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Fixtures))]
    public Task GenerateV2_GridFixture_MatchesXsltSnapshot(string fixture)
    {
        var tree = LoadTree(fixture);

        var xslt = Generate(tree);

        AssertStylesheetInvariants(xslt, tree);
        return Verifier.Verify(SnapshotScrubber.ScrubDataUris(xslt))
            .UseDirectory(SnapshotDir)
            .UseFileName($"Xslt.{fixture}");
    }

    // ── (b) normalize HTML snapshot ──────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RenderCases))]
    public Task RenderV2_GridFixtureOnXml_MatchesNormalizedHtmlSnapshot(string fixture, string xml)
    {
        var xslt = Generate(LoadTree(fixture));

        var html = new XsltHtmlRenderer().Render(xslt, LoadXml(xml)).Html;

        var normalized = SnapshotScrubber.ScrubDataUris(HtmlNormalizer.Normalize(html));
        return Verifier.Verify(normalized)
            .UseDirectory(SnapshotDir)
            .UseFileName($"Html.{fixture}.{xml}");
    }

    // ── kapsam korumaları ────────────────────────────────────────────────────

    [Fact]
    public void GridFixtures_AllFixtures_CoverEveryFrontendBlockType()
    {
        var covered = FixtureNames
            .SelectMany(f => LoadTree(f).Blocks.Values)
            .Select(b => b.Type)
            .ToHashSet(StringComparer.Ordinal);

        var missing = FrontendBlockTypes.Where(t => !covered.Contains(t)).ToList();

        Assert.True(missing.Count == 0, $"Golden fixture'larda kapsanmayan blok tipleri: {string.Join(", ", missing)}");
    }

    [Fact]
    public void RenderCases_XmlFixtureDirectory_EveryXmlIsRendered()
    {
        var used = RenderCases.Select(row => (string)row[1]).ToHashSet(StringComparer.Ordinal);

        var onDisk = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Xml"), "*.xml")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.NotEmpty(onDisk);
        Assert.All(onDisk, name => Assert.Contains(name!, used));
    }

    // ── yardımcılar ──────────────────────────────────────────────────────────

    private static BlockTreeV2Dto LoadTree(string fixture)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GridV2", fixture + ".json"));
        // PreviewController isteği ASP.NET Core varsayılanlarıyla (Web: camelCase, büyük/küçük harf duyarsız) bağlar.
        return JsonSerializer.Deserialize<BlockTreeV2Dto>(json, WebJson)
               ?? throw new InvalidOperationException($"Fixture okunamadı: {fixture}");
    }

    private static string LoadXml(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Xml", name + ".xml"));

    private static string Generate(BlockTreeV2Dto tree)
    {
        var (xslt, error) = new XsltGeneratorService().GenerateV2(tree, Assets);
        Assert.Null(error);
        Assert.NotNull(xslt);
        return xslt!;
    }

    private static void AssertStylesheetInvariants(string xslt, BlockTreeV2Dto tree)
    {
        // Tek güvenli derleme yolu — hata fırlatmamalı.
        SecureXslt.Compile(xslt);

        Assert.Contains("<xsl:stylesheet version=\"2.0\"", xslt);
        var n1 = string.Equals(tree.DocumentType, "Despatch", StringComparison.OrdinalIgnoreCase)
            ? "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2"
            : "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        Assert.Contains($"xmlns:n1=\"{n1}\"", xslt);
        Assert.Contains("xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\"", xslt);
        Assert.Contains("xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"", xslt);
        Assert.Contains("xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\"", xslt);
        Assert.Contains("exclude-result-prefixes=\"n1 cbc cac ext\"", xslt);
        Assert.Contains("<xsl:output method=\"html\" encoding=\"UTF-8\"", xslt);

        // Her blok tipi generator'da tanınmalı.
        Assert.DoesNotContain("unknown block type", xslt);
    }
}
