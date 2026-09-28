using System.Text;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Preview;

namespace XsltCraft.Tests;

/// <summary>e-İrsaliye (documentType = "Despatch") grid üretimi: namespace, hazır bloklar ve dönüşüm çıktısı.</summary>
public class DespatchGeneratorTests
{
    private readonly XsltGeneratorService _sut = new();

    private const string DespatchXml = """
        <DespatchAdvice xmlns="urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2"
                        xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                        xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:CustomizationID>TR1.2.1</cbc:CustomizationID>
          <cbc:ProfileID>TEMELIRSALIYE</cbc:ProfileID>
          <cbc:ID>IRS2026000000001</cbc:ID>
          <cbc:UUID>f5e58e8f-691e-4b15-88ce-8a5378066df8</cbc:UUID>
          <cbc:IssueDate>2026-02-25</cbc:IssueDate>
          <cbc:IssueTime>13:39:23</cbc:IssueTime>
          <cbc:DespatchAdviceTypeCode>SEVK</cbc:DespatchAdviceTypeCode>
          <cbc:Note>Irsaliye notu</cbc:Note>
          <cac:DespatchSupplierParty><cac:Party>
            <cac:PartyIdentification><cbc:ID schemeID="VKN">1111111111</cbc:ID></cac:PartyIdentification>
            <cac:PartyName><cbc:Name>Gonderici AS</cbc:Name></cac:PartyName>
          </cac:Party>
          <cac:DespatchContact><cbc:Name>Ali Veli</cbc:Name></cac:DespatchContact></cac:DespatchSupplierParty>
          <cac:DeliveryCustomerParty><cac:Party>
            <cac:PartyIdentification><cbc:ID schemeID="TCKN">22222222222</cbc:ID></cac:PartyIdentification>
            <cac:PartyName><cbc:Name>Alici Ltd</cbc:Name></cac:PartyName>
          </cac:Party></cac:DeliveryCustomerParty>
          <cac:Shipment>
            <cbc:ID>1</cbc:ID>
            <cac:GoodsItem><cbc:ValueAmount currencyID="TRY">1147.18545665</cbc:ValueAmount></cac:GoodsItem>
            <cac:ShipmentStage>
              <cac:TransportMeans><cac:RoadTransport><cbc:LicensePlateID schemeID="PLAKA">34 ABC 001</cbc:LicensePlateID></cac:RoadTransport></cac:TransportMeans>
              <cac:DriverPerson><cbc:FirstName>Ahmet</cbc:FirstName><cbc:FamilyName>Yilmaz</cbc:FamilyName><cbc:NationalityID>33333333333</cbc:NationalityID></cac:DriverPerson>
            </cac:ShipmentStage>
            <cac:Delivery>
              <cac:CarrierParty>
                <cac:PartyIdentification><cbc:ID schemeID="VKN">4444444444</cbc:ID></cac:PartyIdentification>
                <cac:PartyName><cbc:Name>Tasiyici AS</cbc:Name></cac:PartyName>
              </cac:CarrierParty>
              <cac:Despatch><cbc:ActualDespatchDate>2026-02-26</cbc:ActualDespatchDate><cbc:ActualDespatchTime>14:06:25</cbc:ActualDespatchTime></cac:Despatch>
            </cac:Delivery>
          </cac:Shipment>
          <cac:DespatchLine>
            <cbc:ID>1</cbc:ID>
            <cbc:DeliveredQuantity unitCode="C62">5</cbc:DeliveredQuantity>
            <cac:Item><cbc:Name>Kalem Urun</cbc:Name></cac:Item>
            <cac:Shipment><cac:GoodsItem><cac:InvoiceLine>
              <cbc:LineExtensionAmount currencyID="TRY">503.76</cbc:LineExtensionAmount>
            </cac:InvoiceLine></cac:GoodsItem></cac:Shipment>
          </cac:DespatchLine>
        </DespatchAdvice>
        """;

    private static string Block(string id, string type, string config) =>
        $$"""
        "{{id}}": { "id": "{{id}}", "type": "{{type}}", "config": {{config}},
          "gridLayout": { "x": 10, "y": 10, "width": 180, "height": 20, "autoHeight": true } }
        """;

    private const string HeaderCfg = """{ "fields": [], "title": "", "showTitle": false, "bordered": true, "labelStyle": "table" }""";

    private const string LineTableCfg = """
        { "iterateOver": "//cac:DespatchLine", "showHeader": true, "showRowNumber": false, "showCurrency": true, "bordered": true,
          "columns": [
            { "key": "name", "header": "Mal", "relativeXpath": "cac:Item/cbc:Name", "format": "text", "visible": true, "order": 0 },
            { "key": "qty", "header": "Miktar", "relativeXpath": "cbc:DeliveredQuantity", "format": "quantityWithUnit", "visible": true, "order": 1 },
            { "key": "amount", "header": "Tutar", "relativeXpath": "cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount", "format": "currency", "visible": true, "order": 2 }
          ] }
        """;

    private const string TotalsCfg = """
        { "showCurrency": true, "currencyXpath": "//cac:Shipment/cac:GoodsItem/cbc:ValueAmount/@currencyID",
          "fields": [ { "key": "total", "label": "Toplam Tutar", "xpath": "//cac:Shipment/cac:GoodsItem/cbc:ValueAmount", "visible": true, "highlight": true, "bold": true, "order": 0 } ] }
        """;

    private const string KarekodCfg = """{ "qrWidth": 150, "qrHeight": 150, "qrAlignment": "right" }""";
    private const string LogoCfg = """{ "alignment": "center" }""";
    private const string ShipmentCfg = """{ "fields": [], "title": "TAŞIYICI BİLGİLERİ", "showTitle": true, "bordered": true, "labelStyle": "table" }""";
    private const string NotesCfg = """{ "iterateOver": "//n1:DespatchAdvice/cbc:Note", "prefix": "", "staticLines": [] }""";

    private static string DespatchTree()
    {
        var blocks = string.Join(",\n", [
            Block("aaaaaaaa-0001", "InvoiceHeader", HeaderCfg),
            Block("aaaaaaaa-0002", "InvoiceLineTable", LineTableCfg),
            Block("aaaaaaaa-0003", "InvoiceTotals", TotalsCfg),
            Block("aaaaaaaa-0004", "GibKarekod", KarekodCfg),
            Block("aaaaaaaa-0005", "GibLogo", LogoCfg),
            Block("aaaaaaaa-0006", "ShipmentInfo", ShipmentCfg),
            Block("aaaaaaaa-0007", "Notes", NotesCfg),
        ]);
        return "{ \"version\": 2, \"documentType\": \"Despatch\", \"blocks\": {\n" + blocks + "\n} }";
    }

    private static string Transform(string xslt, string xml)
    {
        var transform = new XslCompiledTransform();
        using (var xr = XmlReader.Create(new StringReader(xslt)))
            transform.Load(xr, new XsltSettings(enableDocumentFunction: false, enableScript: false), null);
        var sb = new StringBuilder();
        using var xmlReader = XmlReader.Create(new StringReader(xml));
        using var writer = new StringWriter(sb);
        transform.Transform(xmlReader, null, writer);
        return sb.ToString();
    }

    [Fact]
    public void Despatch_BindsN1ToDespatchAdviceNamespace()
    {
        var (xslt, error) = _sut.GenerateFromJson(DespatchTree());

        Assert.Null(error);
        Assert.Contains("xmlns:n1=\"urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2\"", xslt);
        Assert.DoesNotContain("schema:xsd:Invoice-2", xslt);
    }

    [Fact]
    public void Invoice_KeepsInvoiceNamespace_WhenDocumentTypeMissing()
    {
        var json = DespatchTree().Replace("\"documentType\": \"Despatch\",", string.Empty);

        var (xslt, error) = _sut.GenerateFromJson(json);

        Assert.Null(error);
        Assert.Contains("schema:xsd:Invoice-2", xslt);
    }

    [Fact]
    public void Despatch_TransformRendersHeaderLinesTotalsAndCarrier()
    {
        var (xslt, error) = _sut.GenerateFromJson(DespatchTree());
        Assert.Null(error);

        var html = Transform(xslt!, DespatchXml);

        Assert.Contains("İrsaliye No:", html);
        Assert.Contains("IRS2026000000001", html);
        Assert.Contains("TEMELIRSALIYE", html);
        Assert.Contains("SEVK", html);
        Assert.Contains("2026-02-26", html);           // sevk tarihi
        Assert.Contains("Kalem Urun", html);
        Assert.Contains("503,76 TL", html);            // satır tutarı, @currencyID → TL
        Assert.Contains("1.147,19 TL", html);          // toplam tutar
        Assert.Contains("Tasiyici AS", html);
        Assert.Contains("34 ABC 001", html);
        Assert.Contains("Ahmet Yilmaz", html);
        Assert.Contains("Ali Veli", html);             // teslim eden
        Assert.Contains("Irsaliye notu", html);
        Assert.Contains("e-İRSALİYE", html);
        Assert.DoesNotContain("e-FATURA", html);
    }

    [Fact]
    public void Despatch_KarekodPayloadUsesDespatchFields()
    {
        var (xslt, _) = _sut.GenerateFromJson(DespatchTree());

        var html = Transform(xslt!, DespatchXml);

        Assert.Contains("\"vkntckn\":\"1111111111\"", html);
        Assert.Contains("\"avkntckn\":\"22222222222\"", html);
        Assert.Contains("\"tip\":\"SEVK\"", html);
        Assert.Contains("\"sevktarihi\":\"2026-02-26\"", html);
        Assert.Contains("\"tasiyicivkn\":\"4444444444\"", html);
        Assert.Contains("\"plaka\":\"34 ABC 001\"", html);
    }
}
