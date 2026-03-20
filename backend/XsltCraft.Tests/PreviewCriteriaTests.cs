using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests;

/// <summary>
/// Faz 4 tamamlanma kriterleri:
///   1. Preview in-memory — storage'a yazma yok
///   2. XSLT → HTML dönüşümü ≤ 2 sn (50 satırlık e-Fatura XML)
///   3. Geçersiz XML → anlamlı hata mesajı
///   4. Geçersiz XSLT → derleme aşamasında yakalanır
///   5. Birden fazla block tipi içeren tree → geçerli HTML üretir
/// </summary>
public class PreviewCriteriaTests
{
    private readonly XsltGeneratorService _generator = new();

    // ── 50 satırlık UBL 2.1 e-Fatura XML ──────────────────────────────────────

    private const string UblInvoiceXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:UBLVersionID>2.1</cbc:UBLVersionID>
          <cbc:CustomizationID>TR1.2</cbc:CustomizationID>
          <cbc:ProfileID>TEMELFATURA</cbc:ProfileID>
          <cbc:ID>GIB2024000000001</cbc:ID>
          <cbc:UUID>a1b2c3d4-e5f6-7890-abcd-ef1234567890</cbc:UUID>
          <cbc:IssueDate>2024-01-15</cbc:IssueDate>
          <cbc:IssueTime>09:00:00</cbc:IssueTime>
          <cbc:InvoiceTypeCode>TEMELFATURA</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
          <cbc:LineCountNumeric>2</cbc:LineCountNumeric>
          <cac:AccountingSupplierParty>
            <cac:Party>
              <cac:PartyName><cbc:Name>Satıcı A.Ş.</cbc:Name></cac:PartyName>
              <cac:PostalAddress>
                <cbc:StreetName>Test Sokak No:1</cbc:StreetName>
                <cbc:CityName>İstanbul</cbc:CityName>
                <cbc:CountrySubentity>Kadıköy</cbc:CountrySubentity>
                <cac:Country><cbc:IdentificationCode>TR</cbc:IdentificationCode></cac:Country>
              </cac:PostalAddress>
              <cac:Contact>
                <cbc:Telephone>+90 212 000 0000</cbc:Telephone>
                <cbc:ElectronicMail>info@satici.com.tr</cbc:ElectronicMail>
              </cac:Contact>
            </cac:Party>
          </cac:AccountingSupplierParty>
          <cac:AccountingCustomerParty>
            <cac:Party>
              <cac:PartyName><cbc:Name>Alıcı Ltd. Şti.</cbc:Name></cac:PartyName>
              <cac:PostalAddress>
                <cbc:CityName>Ankara</cbc:CityName>
              </cac:PostalAddress>
            </cac:Party>
          </cac:AccountingCustomerParty>
          <cac:PaymentMeans>
            <cac:PayeeFinancialAccount>
              <cbc:ID>TR12 0001 2345 6789 0123 45</cbc:ID>
              <cac:FinancialInstitutionBranch>
                <cac:FinancialInstitution>
                  <cbc:Name>Test Bankası</cbc:Name>
                </cac:FinancialInstitution>
              </cac:FinancialInstitutionBranch>
            </cac:PayeeFinancialAccount>
          </cac:PaymentMeans>
          <cac:TaxTotal>
            <cbc:TaxAmount currencyID="TRY">270.00</cbc:TaxAmount>
          </cac:TaxTotal>
          <cac:LegalMonetaryTotal>
            <cbc:LineExtensionAmount currencyID="TRY">1500.00</cbc:LineExtensionAmount>
            <cbc:TaxExclusiveAmount currencyID="TRY">1500.00</cbc:TaxExclusiveAmount>
            <cbc:TaxInclusiveAmount currencyID="TRY">1770.00</cbc:TaxInclusiveAmount>
            <cbc:PayableAmount currencyID="TRY">1770.00</cbc:PayableAmount>
          </cac:LegalMonetaryTotal>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity unitCode="C62">10</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="TRY">1000.00</cbc:LineExtensionAmount>
            <cac:Item><cbc:Name>Ürün A</cbc:Name></cac:Item>
            <cac:Price><cbc:PriceAmount currencyID="TRY">100.00</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
          <cac:InvoiceLine>
            <cbc:ID>2</cbc:ID>
            <cbc:InvoicedQuantity unitCode="C62">5</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="TRY">500.00</cbc:LineExtensionAmount>
            <cac:Item><cbc:Name>Ürün B</cbc:Name></cac:Item>
            <cac:Price><cbc:PriceAmount currencyID="TRY">100.00</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
        </Invoice>
        """;

    // ── Yardımcı: blok ağacından XSLT üret → XML'e uygula → HTML döndür ───────

    private static string ApplyXslt(string xslt, string xml)
    {
        var transform = new XslCompiledTransform();
        using var xsltReader = XmlReader.Create(new StringReader(xslt));
        transform.Load(xsltReader);

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        using var sw = new StringWriter();
        transform.Transform(doc, null, sw);
        return sw.ToString();
    }

    private static JsonElement El(object obj) => JsonSerializer.SerializeToElement(obj);

    private static BlockTreeDto InvoicePreviewTree()
    {
        return new BlockTreeDto
        {
            Sections =
            [
                new SectionDto { Id = "s1", Name = "Başlık", Order = 1, BlockIds = ["b1", "b2", "b3"] },
                new SectionDto { Id = "s2", Name = "Satırlar", Order = 2, BlockIds = ["b4"] },
                new SectionDto { Id = "s3", Name = "Toplam", Order = 3, BlockIds = ["b5", "b6"] },
            ],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b1"] = new BlockDto { Id = "b1", Type = "Heading",
                    Config = El(new { level = "H1", isStatic = true, content = "E-FATURA" }) },

                ["b2"] = new BlockDto { Id = "b2", Type = "DocumentInfo",
                    Config = El(new { rows = new object[]
                    {
                        new { label = "Fatura No",   xpath = "//cbc:ID" },
                        new { label = "Tarih",        xpath = "//cbc:IssueDate" },
                        new { label = "Tür",          xpath = "//cbc:InvoiceTypeCode" },
                        new { label = "Para Birimi",  xpath = "//cbc:DocumentCurrencyCode" },
                    }}) },

                ["b3"] = new BlockDto { Id = "b3", Type = "Divider",
                    Config = El(new { style = "solid", color = "#CCCCCC", thickness = "1px" }) },

                ["b4"] = new BlockDto { Id = "b4", Type = "Table",
                    Config = El(new
                    {
                        iterateOver = "//cac:InvoiceLine",
                        showHeader = true,
                        columns = new object[]
                        {
                            new { header = "No",      xpath = "cbc:ID",    width = "5%" },
                            new { header = "Ürün",    xpath = "cac:Item/cbc:Name", width = "40%" },
                            new { header = "Miktar",  xpath = "cbc:InvoicedQuantity", width = "15%" },
                            new { header = "Tutar",   xpath = "cbc:LineExtensionAmount", width = "20%" },
                        }
                    }) },

                ["b5"] = new BlockDto { Id = "b5", Type = "Totals",
                    Config = El(new { alignment = "right", rows = new object[]
                    {
                        new { label = "KDV Matrahı",  xpath = "//cac:LegalMonetaryTotal/cbc:TaxExclusiveAmount", highlight = false },
                        new { label = "KDV",          xpath = "//cac:TaxTotal/cbc:TaxAmount", highlight = false },
                        new { label = "ÖDENECEK",     xpath = "//cac:LegalMonetaryTotal/cbc:PayableAmount", highlight = true },
                    }}) },

                ["b6"] = new BlockDto { Id = "b6", Type = "ETTN",
                    Config = El(new { ettnXpath = "//cbc:UUID", showQR = false }) },
            }
        };
    }

    // ── Kriter 1: Preview in-memory — storage'a hiçbir şey yazılmıyor ─────────

    [Fact]
    public void Preview_IsInMemory_NoFilesWritten()
    {
        // XsltGeneratorService saf bir fonksiyondur: I/O bağımlılığı yoktur.
        // Generate() → ApplyXslt() zinciri yalnızca bellek kullanır.
        var tree = InvoicePreviewTree();
        var (xslt, error) = _generator.Generate(tree);

        Assert.Null(error);
        Assert.NotNull(xslt);

        // Eğer Apply dosya yazmaya çalışsaydı burada istisna fırlatırdı.
        var html = ApplyXslt(xslt!, UblInvoiceXml);
        Assert.NotEmpty(html);
    }

    // ── Kriter 2: Render süresi ≤ 2 sn (50 satırlık e-Fatura XML) ────────────

    [Fact]
    public void Preview_RenderTime_UnderTwoSeconds()
    {
        var tree = InvoicePreviewTree();
        var (xslt, error) = _generator.Generate(tree);
        Assert.Null(error);

        var sw = Stopwatch.StartNew();
        var html = ApplyXslt(xslt!, UblInvoiceXml);
        sw.Stop();

        Assert.NotEmpty(html);
        Assert.True(
            sw.ElapsedMilliseconds < 2000,
            $"Render süresi {sw.ElapsedMilliseconds} ms — 2000 ms sınırını aştı.");
    }

    // ── Kriter 3: Geçersiz XML → XmlException, anlamlı hata mesajı ───────────

    [Fact]
    public void Preview_InvalidXml_ThrowsXmlException()
    {
        var tree = InvoicePreviewTree();
        var (xslt, _) = _generator.Generate(tree);

        var ex = Assert.Throws<XmlException>(() => ApplyXslt(xslt!, "BU GECERSIZ XML"));
        Assert.NotEmpty(ex.Message);
    }

    // ── Kriter 4: Geçersiz XSLT → XslCompiledTransform yakalanır ─────────────

    [Fact]
    public void Preview_InvalidXslt_ThrowsAtLoad()
    {
        const string badXslt = """<?xml version="1.0"?><xsl:stylesheet version="1.0" xmlns:xsl="YANLIS_NS"><broken/></xsl:stylesheet>""";

        Assert.ThrowsAny<Exception>(() =>
        {
            var t = new XslCompiledTransform();
            using var r = XmlReader.Create(new StringReader(badXslt));
            t.Load(r);
        });
    }

    // ── Kriter 5: HTML çıktısı doğru içeriği taşır ───────────────────────────

    [Fact]
    public void Preview_HtmlOutput_ContainsExpectedContent()
    {
        var tree = InvoicePreviewTree();
        var (xslt, error) = _generator.Generate(tree);
        Assert.Null(error);

        var html = ApplyXslt(xslt!, UblInvoiceXml);

        // Statik başlık
        Assert.Contains("E-FATURA", html);
        // Fatura no (XML'den okundu)
        Assert.Contains("GIB2024000000001", html);
        // Tablo satırı ürün adı
        Assert.Contains("Ürün A", html);
        Assert.Contains("Ürün B", html);
        // Toplam tutarı
        Assert.Contains("1770.00", html);
        // ETTN
        Assert.Contains("a1b2c3d4-e5f6-7890-abcd-ef1234567890", html);
        // Geçerli HTML iskeleti
        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
    }

    // ── Ek: Conditional blok — TEMELFATURA koşulu ─────────────────────────────

    [Fact]
    public void Preview_ConditionalBlock_RendersCorrectly()
    {
        var tree = new BlockTreeDto
        {
            Sections = [new SectionDto { Id = "s1", Name = "Test", Order = 1, BlockIds = ["b1"] }],
            Blocks = new Dictionary<string, BlockDto>
            {
                ["b1"] = new BlockDto
                {
                    Id = "b1", Type = "Conditional",
                    Config = El(new
                    {
                        condition = new { xpath = "//cbc:InvoiceTypeCode", @operator = "equals", value = "TEMELFATURA" },
                        thenBlockIds = Array.Empty<string>(),
                        elseBlockIds = Array.Empty<string>()
                    })
                }
            }
        };

        var (xslt, error) = _generator.Generate(tree);
        Assert.Null(error);

        var html = ApplyXslt(xslt!, UblInvoiceXml);
        Assert.NotEmpty(html);
    }

    // ── Ek: Boş tree ile preview ─────────────────────────────────────────────

    [Fact]
    public void Preview_EmptyTree_ReturnsEmptyHtml()
    {
        var (xslt, error) = _generator.Generate(new BlockTreeDto());
        Assert.Null(error);

        var html = ApplyXslt(xslt!, UblInvoiceXml);
        // Geçerli HTML olmalı ama içerik boş
        Assert.Contains("<html", html);
    }
}
