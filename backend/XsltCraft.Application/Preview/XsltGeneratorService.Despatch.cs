using System.Text;
using System.Text.RegularExpressions;

namespace XsltCraft.Application.Preview;

// e-İrsaliye (UBL-TR DespatchAdvice) üretimi — fatura bloklarının irsaliye karşılıkları.
public sealed partial class XsltGeneratorService
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string DespatchNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";

    private static bool IsDespatch(string? documentType) =>
        string.Equals(documentType, "Despatch", StringComparison.OrdinalIgnoreCase);

    /// <summary>TRY/TRL → TL, diğer kodlar olduğu gibi. <paramref name="currencyXpath"/> XML-attr kaçışlı olmalı.</summary>
    private static string CurrencySuffixFrom(string currencyXpath) =>
        $"<xsl:text> </xsl:text><xsl:choose><xsl:when test=\"{currencyXpath}='TRY' or {currencyXpath}='TRL'\">TL</xsl:when>" +
        $"<xsl:otherwise><xsl:value-of select=\"{currencyXpath}\"/></xsl:otherwise></xsl:choose>";

    // ── InvoiceHeader (irsaliye) — İrsaliye bilgileri tablosu ─────────────

    private static string GenerateDespatchHeader(BlockDto block)
    {
        var cfg = Deserialize<InvoiceHeaderConfig>(block.Config);
        var fontSize = string.IsNullOrWhiteSpace(cfg.FontSize) ? "10.4px" : XmlEscape(cfg.FontSize);
        var bdrStyle = string.IsNullOrWhiteSpace(cfg.BorderStyle) ? "solid" : XmlEscape(cfg.BorderStyle);
        var tableBorder = cfg.Bordered ? $"border:1px {bdrStyle} #555555;" : string.Empty;
        var cellBorder = cfg.Bordered ? $"border:1px {bdrStyle} #555555;" : string.Empty;
        var scopeId = "dh" + block.Id.Replace("-", string.Empty)[..8];

        var sb = new StringBuilder();
        sb.AppendLine($"    <div style=\"font-size:{fontSize}\">");
        sb.AppendLine($"      <style>.{scopeId} td {{ {cellBorder}padding:2px 6px; }}</style>");
        if (cfg.ShowTitle && !string.IsNullOrWhiteSpace(cfg.Title))
            sb.AppendLine($"      <p style=\"font-weight:bold;margin:0 0 4px 0;\">{XmlEscape(cfg.Title)}</p>");
        sb.AppendLine($"      <table class=\"{scopeId}\" style=\"width:100%;border-collapse:collapse;{tableBorder}\">");
        sb.AppendLine("        <tbody>");

        AppendHeaderRow(sb, "Özelleştirme No:", "<xsl:value-of select=\"n1:DespatchAdvice/cbc:CustomizationID\"/>");
        AppendHeaderRow(sb, "Senaryo:", "<xsl:value-of select=\"n1:DespatchAdvice/cbc:ProfileID\"/>");
        AppendHeaderRow(sb, "İrsaliye Tipi:", "<xsl:value-of select=\"n1:DespatchAdvice/cbc:DespatchAdviceTypeCode\"/>");
        AppendHeaderRow(sb, "İrsaliye No:", "<xsl:value-of select=\"n1:DespatchAdvice/cbc:ID\"/>");
        AppendHeaderRow(sb, "İrsaliye Tarihi:",
            "<xsl:value-of select=\"n1:DespatchAdvice/cbc:IssueDate\"/><xsl:text>&#160;</xsl:text>" +
            "<xsl:value-of select=\"substring(n1:DespatchAdvice/cbc:IssueTime,1,5)\"/>");

        const string despatchNode = "n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:Despatch";
        AppendHeaderRow(sb, "Sevk Tarihi:",
            $"<xsl:value-of select=\"{despatchNode}/cbc:ActualDespatchDate\"/><xsl:text>&#160;</xsl:text>" +
            $"<xsl:value-of select=\"substring({despatchNode}/cbc:ActualDespatchTime,1,5)\"/>",
            condition: $"{despatchNode}/cbc:ActualDespatchDate");
        AppendHeaderRow(sb, "Sipariş No:", "<xsl:value-of select=\"n1:DespatchAdvice/cac:OrderReference/cbc:ID\"/>",
            condition: "n1:DespatchAdvice/cac:OrderReference/cbc:ID");
        AppendHeaderRow(sb, "Sipariş Tarihi:", "<xsl:value-of select=\"n1:DespatchAdvice/cac:OrderReference/cbc:IssueDate\"/>",
            condition: "n1:DespatchAdvice/cac:OrderReference/cbc:IssueDate");

        AppendCustomRows(sb, cfg);

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.Append("    </div>");
        return sb.ToString();
    }

    private static void AppendHeaderRow(StringBuilder sb, string label, string valueXslt, string? condition = null)
    {
        var row =
            $"          <tr style=\"height:13px;\"><td style=\"width:50%;\"><span style=\"font-weight:bold;\"><xsl:text>{XmlEscape(label)}</xsl:text></span></td>" +
            $"<td style=\"width:50%;\">{valueXslt}</td></tr>";
        if (condition is null)
        {
            sb.AppendLine(row);
            return;
        }
        sb.AppendLine($"          <xsl:if test=\"{condition}\">");
        sb.AppendLine(row);
        sb.AppendLine("          </xsl:if>");
    }

    private static void AppendCustomRows(StringBuilder sb, InvoiceHeaderConfig cfg)
    {
        foreach (var f in (cfg.Fields ?? []).Where(f => f.Visible && f.IsCustom == true).OrderBy(f => f.Order))
        {
            var xp = XmlAttr(f.Xpath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(xp)) continue;
            AppendHeaderRow(sb, f.Label ?? string.Empty, $"<xsl:value-of select=\"{xp}\"/>");
        }
    }

    // ── ShipmentInfo — Taşıyıcı / araç / şoför bilgileri ──────────────────

    private static string GenerateShipmentInfo(BlockDto block)
    {
        var cfg = Deserialize<InvoiceHeaderConfig>(block.Config);
        var fontSize = string.IsNullOrWhiteSpace(cfg.FontSize) ? "10.4px" : XmlEscape(cfg.FontSize);
        var bdrStyle = string.IsNullOrWhiteSpace(cfg.BorderStyle) ? "solid" : XmlEscape(cfg.BorderStyle);
        var outerStyle = cfg.Bordered ? $"border:1px {bdrStyle} #555555;padding:4px 8px;" : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"    <div style=\"font-size:{fontSize};{outerStyle}\">");
        if (cfg.ShowTitle && !string.IsNullOrWhiteSpace(cfg.Title))
            sb.AppendLine($"      <p style=\"font-weight:bold;margin:0 0 4px 0;\">{XmlEscape(cfg.Title)}</p>");
        sb.AppendLine("      <table style=\"width:100%;border-collapse:collapse;\">");
        sb.AppendLine("        <tbody>");

        AppendShipmentRow(sb, "Taşıyıcı Firma:", "//cac:Shipment/cac:Delivery/cac:CarrierParty",
            "<xsl:value-of select=\"cac:PartyName/cbc:Name\"/><xsl:text> (VKN: </xsl:text>" +
            "<xsl:value-of select=\"cac:PartyIdentification/cbc:ID\"/><xsl:text>)</xsl:text>");
        AppendShipmentRow(sb, "Araç Plaka No:",
            "//cac:ShipmentStage/cac:TransportMeans/cac:RoadTransport/cbc:LicensePlateID",
            "<xsl:value-of select=\".\"/>");
        AppendShipmentRow(sb, "Dorse Plaka No:",
            "//cac:TransportHandlingUnit/cac:TransportEquipment/cbc:ID[@schemeID='DORSEPLAKA']",
            "<xsl:value-of select=\".\"/>");
        AppendShipmentRow(sb, "Şoför:", "//cac:ShipmentStage/cac:DriverPerson[cbc:FirstName]",
            "<xsl:value-of select=\"cbc:FirstName\"/><xsl:text> </xsl:text><xsl:value-of select=\"cbc:FamilyName\"/>" +
            "<xsl:if test=\"cbc:NationalityID\"><xsl:text>, TCKN: </xsl:text><xsl:value-of select=\"cbc:NationalityID\"/></xsl:if>");
        AppendShipmentRow(sb, "Teslim Eden:", "//cac:DespatchSupplierParty/cac:DespatchContact/cbc:Name",
            "<xsl:value-of select=\".\"/>");

        AppendCustomRows(sb, cfg);

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.Append("    </div>");
        return sb.ToString();
    }

    private static void AppendShipmentRow(StringBuilder sb, string label, string forEachXpath, string valueXslt)
    {
        sb.AppendLine($"          <xsl:for-each select=\"{forEachXpath}\">");
        sb.AppendLine(
            $"            <tr><td style=\"font-weight:bold;padding:1px 4px;white-space:nowrap;width:35%;vertical-align:top\">{XmlEscape(label)}</td>" +
            $"<td style=\"padding:1px 4px\">{valueXslt}</td></tr>");
        sb.AppendLine("          </xsl:for-each>");
    }

    // ── GibKarekod (irsaliye) — GİB e-İrsaliye karekod içeriği ────────────

    private static string GenerateDespatchKarekod(BlockDto block)
    {
        var cfg = Deserialize<GibKarekodConfig>(block.Config);
        var safeId = Regex.Replace(block.Id, "[^a-zA-Z0-9]", "");
        var qrWidth = cfg.QrWidth > 0 ? cfg.QrWidth : 150;
        var qrHeight = cfg.QrHeight > 0 ? cfg.QrHeight : 150;
        var flexJustify = QrFlexJustify(cfg.QrAlignment);
        var qrOpacity = cfg.Opacity is int qop && qop < 100 ? FormattableString.Invariant($"opacity:{qop / 100.0:F2};") : string.Empty;

        const string d = "n1:DespatchAdvice";
        const string idFilter = "cbc:ID[@schemeID='TCKN' or @schemeID='VKN']";
        var payload =
            $"{{\"vkntckn\":\"<xsl:value-of select=\"{d}/cac:DespatchSupplierParty/cac:Party/cac:PartyIdentification/{idFilter}\"/>\"," +
            $"\"avkntckn\":\"<xsl:value-of select=\"{d}/cac:DeliveryCustomerParty/cac:Party/cac:PartyIdentification/{idFilter}\"/>\"," +
            $"\"senaryo\":\"<xsl:value-of select=\"{d}/cbc:ProfileID\"/>\"," +
            $"\"tip\":\"<xsl:value-of select=\"{d}/cbc:DespatchAdviceTypeCode\"/>\"," +
            $"\"tarih\":\"<xsl:value-of select=\"{d}/cbc:IssueDate\"/>\"," +
            $"\"no\":\"<xsl:value-of select=\"{d}/cbc:ID\"/>\"," +
            $"\"ettn\":\"<xsl:value-of select=\"{d}/cbc:UUID\"/>\"," +
            $"\"sevktarihi\":\"<xsl:value-of select=\"{d}/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchDate\"/>\"," +
            $"\"sevkzamani\":\"<xsl:value-of select=\"{d}/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchTime\"/>\"," +
            $"\"tasiyicivkn\":\"<xsl:value-of select=\"{d}/cac:Shipment/cac:Delivery/cac:CarrierParty/cac:PartyIdentification/cbc:ID[@schemeID='VKN']\"/>\"," +
            $"\"plaka\":\"<xsl:value-of select=\"{d}/cac:Shipment/cac:ShipmentStage/cac:TransportMeans/cac:RoadTransport/cbc:LicensePlateID[@schemeID='PLAKA']\"/>\"}}";

        var sb = new StringBuilder();
        sb.AppendLine($"    <div class=\"ettn\" style=\"{qrOpacity}\">");
        sb.AppendLine("      <div id=\"qrvalue\" style=\"visibility: hidden; height: 20px;width: 20px; ; display:none\">");
        sb.AppendLine($"        {payload}");
        sb.AppendLine("      </div>");
        sb.AppendLine($"      <div style=\"display:flex; justify-content:{flexJustify};\">");
        sb.AppendLine($"        <div id=\"qrc{safeId}\" style=\"line-height:0; flex-shrink:0;\"></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <script type=\"text/javascript\">");
        sb.AppendLine("        window.addEventListener('load', function() {");
        sb.AppendLine("          var v = document.getElementById('qrvalue');");
        sb.AppendLine($"          var c = document.getElementById('qrc{safeId}');");
        sb.AppendLine("          if (!v || !c || typeof QRCode === 'undefined') return;");
        sb.AppendLine("          var t = v.textContent ? v.textContent.trim() : '';");
        sb.AppendLine("          if (!t) return;");
        sb.AppendLine($"          new QRCode(c, {{ text: t, width: {qrWidth}, height: {qrHeight}, correctLevel: QRCode.CorrectLevel.M }});");
        sb.AppendLine("        });");
        sb.AppendLine("      </script>");
        sb.Append("    </div>");
        return sb.ToString();
    }
}
