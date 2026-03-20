using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Preview;

public sealed class XsltGeneratorService : IXsltGeneratorService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Public API ────────────────────────────────────────────────────────

    public (string? Xslt, string? Error) GenerateFromJson(string blockTreeJson)
    {
        BlockTreeDto tree;
        try
        {
            tree = JsonSerializer.Deserialize<BlockTreeDto>(blockTreeJson, JsonOpts)
                   ?? throw new JsonException("Boş sonuç.");
        }
        catch (Exception ex)
        {
            return (null, $"Block tree JSON parse hatası: {ex.Message}");
        }

        return Generate(tree);
    }

    public (string? Xslt, string? Error) Generate(BlockTreeDto tree)
    {
        try
        {
            var body = BuildBody(tree);

            // QR kütüphanesi gereken blok var mı?
            var hasQr = tree.Blocks.Values.Any(b =>
                b.Type == "GibKarekod" ||
                (b.Type == "ETTN" && Deserialize<EttnConfig>(b.Config).ShowQR));

            var xslt = WrapStylesheet(body, hasQr);
            var validationError = Validate(xslt);
            return validationError is null ? (xslt, null) : (null, validationError);
        }
        catch (Exception ex)
        {
            return (null, $"XSLT üretim hatası: {ex.Message}");
        }
    }

    // ── Stylesheet wrapper ────────────────────────────────────────────────
    // NOT: CSS içindeki { ve } karakterleri ile C# string interpolasyonu çakışmasını
    // önlemek için metni parçalara bölüp birleştiriyoruz.

    private static string WrapStylesheet(string body, bool includeQrLib = false)
    {
        var qrScript = includeQrLib
            ? "    <script src=\"https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js\"></script>\n"
            : string.Empty;

        const string header =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <xsl:stylesheet version="1.0"
              xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
              xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
              xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
              xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
              xmlns:ext="urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"
              exclude-result-prefixes="n1 cbc cac ext">

              <xsl:output method="html" encoding="UTF-8" indent="yes"/>

              <xsl:template match="/">
                <html>
                  <head>
                    <meta charset="UTF-8"/>
            """;

        const string styles =
            """
                    <style>
                      * { box-sizing: border-box; }
                      html, body { margin: 0; padding: 0; background: #d1d5db; font-family: Arial, sans-serif; font-size: 10pt; color: #111; }
                      .page {
                        background: #ffffff;
                        width: 210mm;
                        min-height: 297mm;
                        margin: 16px auto;
                        padding: 12mm 14mm 18mm 14mm;
                        box-shadow: 0 2px 12px rgba(0,0,0,0.18);
                      }
                      /* layout tables — multi-column rows, no visible borders */
                      table.lr { width: 100%; border-collapse: collapse; table-layout: fixed; margin: 0; }
                      table.lr > tbody > tr > td {
                        border: none;
                        padding: 0 4px;
                        vertical-align: top;
                        word-break: break-word;
                        overflow-wrap: break-word;
                      }
                      /* data tables — Table block */
                      table.dt { width: 100%; border-collapse: collapse; table-layout: fixed; margin-bottom: 6px; font-size: 9pt; }
                      table.dt th { border: 1px solid #333; padding: 3px 6px; background: #d0d0d0; font-weight: bold; text-align: center; overflow: hidden; }
                      table.dt td { border: 1px solid #555; padding: 3px 6px; word-break: break-word; overflow-wrap: break-word; overflow: hidden; }
                      /* document info table — key-value */
                      table.di { width: 100%; border-collapse: collapse; margin-bottom: 4px; font-size: 9.5pt; }
                      table.di td { border: 1px solid #555; padding: 2px 6px; }
                      table.di td:first-child { font-weight: bold; white-space: nowrap; width: 40%; }
                      table.di-plain { width: 100%; border-collapse: collapse; margin-bottom: 4px; font-size: 9.5pt; }
                      table.di-plain td { border: none; padding: 2px 6px; }
                      table.di-plain td:first-child { font-weight: bold; white-space: nowrap; width: 40%; }
                      /* totals table */
                      table.tot { border-collapse: collapse; margin-left: auto; margin-bottom: 4px; font-size: 9.5pt; min-width: 280px; }
                      table.tot td { border: 1px solid #aaa; padding: 2px 8px; }
                      table.tot td.lbl { text-align: right; padding-right: 12px; white-space: nowrap; }
                      table.tot td.val { text-align: right; white-space: nowrap; min-width: 120px; }
                      table.tot tr.hl td { font-weight: bold; background: #ffffcc; }
                      /* typography */
                      h1 { font-size: 16pt; margin: 0 0 6px 0; }
                      h2 { font-size: 13pt; margin: 0 0 4px 0; }
                      h3 { font-size: 11pt; margin: 0 0 4px 0; }
                      h4 { font-size: 10pt; margin: 0 0 4px 0; }
                      p  { margin: 0 0 4px 0; line-height: 1.4; }
                      hr { border: none; margin: 6px 0; }
                    </style>
            """;

        const string bodyOpen =
            """
                  </head>
                  <body>
                    <div class="page">
            """;

        const string footer =
            """
                    </div>
                  </body>
                </html>
              </xsl:template>

            </xsl:stylesheet>
            """;

        return header + qrScript + styles + "\n" + bodyOpen + "\n" + body + "\n" + footer;
    }

    // ── Body builder ──────────────────────────────────────────────────────
    // Consecutive same-width blocks are grouped into HTML <table> rows.

    private string BuildBody(BlockTreeDto tree)
    {
        var sb = new StringBuilder();
        foreach (var section in tree.Sections.OrderBy(s => s.Order))
        {
            sb.AppendLine($"    <!-- section: {XmlEscape(section.Name)} -->");

            var blockList = section.BlockIds
                .Select(id => tree.Blocks.TryGetValue(id, out var b) ? b : null)
                .Where(b => b is not null)
                .Select(b => b!)
                .ToList();

            int i = 0;
            while (i < blockList.Count)
            {
                var cur = blockList[i];
                var width = cur.Layout?.Width ?? "full";

                if (width is "1/2" or "1/3" or "2/3")
                {
                    var frac = width;
                    var run = new List<BlockDto>();
                    while (i < blockList.Count && (blockList[i].Layout?.Width ?? "full") == frac)
                        run.Add(blockList[i++]);

                    int cols = frac switch { "1/3" => 3, "2/3" => 2, _ => 2 };
                    string colWidthPct = frac switch { "1/3" => "33.333%", "2/3" => "50%", _ => "50%" };

                    for (int j = 0; j < run.Count; j += cols)
                    {
                        sb.AppendLine("    <table class=\"lr\"><tbody><tr>");
                        for (int k = j; k < j + cols; k++)
                        {
                            if (k < run.Count)
                            {
                                var b = run[k];
                                sb.AppendLine($"      <td style=\"width:{colWidthPct};{TextAlignStyle(b.Layout?.Alignment)}\">{DispatchBlock(b, tree)}</td>");
                            }
                            else
                            {
                                sb.AppendLine($"      <td style=\"width:{colWidthPct}\"></td>");
                            }
                        }
                        sb.AppendLine("    </tr></tbody></table>");
                    }
                }
                else
                {
                    var align = TextAlignStyle(cur.Layout?.Alignment);
                    sb.AppendLine($"    <div style=\"width:100%;{align}\">{DispatchBlock(cur, tree)}</div>");
                    i++;
                }
            }
        }
        return sb.ToString();
    }

    private static string TextAlignStyle(string? alignment) => alignment switch
    {
        "center" => "text-align:center;",
        "right"  => "text-align:right;",
        _        => ""
    };

    private string GenerateBlock(BlockDto block, BlockTreeDto tree) => DispatchBlock(block, tree);

    // ── Block dispatcher ─────────────────────────────────────────────────

    private string DispatchBlock(BlockDto block, BlockTreeDto tree)
    {
        return block.Type switch
        {
            "Text"         => GenerateText(block),
            "Heading"      => GenerateHeading(block),
            "Paragraph"    => GenerateParagraph(block),
            "Table"        => GenerateTable(block),
            "ForEach"      => GenerateForEach(block, tree),
            "Conditional"  => GenerateConditional(block, tree),
            "Image"        => GenerateImage(block),
            "DocumentInfo" => GenerateDocumentInfo(block),
            "Totals"       => GenerateTotals(block),
            "Notes"        => GenerateNotes(block),
            "BankInfo"     => GenerateBankInfo(block),
            "ETTN"         => GenerateETTN(block),
            "Divider"         => GenerateDivider(block),
            "Spacer"          => GenerateSpacer(block),
            "Variable"        => GenerateVariable(block),
            "ConditionalText" => GenerateConditionalText(block),
            "TaxSummary"      => GenerateTaxSummary(block),
            "GibKarekod"      => GenerateGibKarekod(block),
            _                 => $"<!-- unknown block type: {XmlEscape(block.Type)} -->"
        };
    }

    // ── BLOCK-01: Text ────────────────────────────────────────────────────

    private static string GenerateText(BlockDto block)
    {
        var cfg = Deserialize<TextConfig>(block.Config);
        var styleAttr = BuildStyleAttr(cfg.FontWeight, cfg.FontStyle, cfg.FontSize, cfg.Color);

        if (cfg.IsStatic)
            return $"    <p{styleAttr}>{XmlEscape(cfg.Content ?? string.Empty)}</p>";

        var xpath = cfg.Binding?.Xpath ?? string.Empty;
        var fallback = cfg.Binding?.Fallback;
        if (!string.IsNullOrEmpty(fallback))
            return $"""    <p{styleAttr}><xsl:choose><xsl:when test="{XmlAttr(xpath)}"><xsl:value-of select="{XmlAttr(xpath)}"/></xsl:when><xsl:otherwise>{XmlEscape(fallback)}</xsl:otherwise></xsl:choose></p>""";

        return $"""    <p{styleAttr}><xsl:value-of select="{XmlAttr(xpath)}"/></p>""";
    }

    // ── BLOCK-02: Heading ─────────────────────────────────────────────────

    private static string GenerateHeading(BlockDto block)
    {
        var cfg = Deserialize<HeadingConfig>(block.Config);
        var tag = cfg.Level.ToLowerInvariant();
        var styleAttr = BuildStyleAttr(cfg.FontWeight, cfg.FontStyle, cfg.FontSize, cfg.Color);

        if (cfg.IsStatic)
            return $"    <{tag}{styleAttr}>{XmlEscape(cfg.Content ?? string.Empty)}</{tag}>";

        var xpath = cfg.Binding?.Xpath ?? string.Empty;
        return $"""    <{tag}{styleAttr}><xsl:value-of select="{XmlAttr(xpath)}"/></{tag}>""";
    }

    // ── BLOCK-03: Paragraph ───────────────────────────────────────────────

    private static string GenerateParagraph(BlockDto block)
    {
        var cfg = Deserialize<ParagraphConfig>(block.Config);
        var sb = new StringBuilder("    <p>");
        foreach (var line in cfg.Lines)
        {
            if (line.IsStatic)
                sb.Append(XmlEscape(line.Content ?? string.Empty));
            else
                sb.Append($"""<xsl:value-of select="{XmlAttr(line.Xpath ?? string.Empty)}"/>""");
        }
        sb.Append("</p>");
        return sb.ToString();
    }

    // ── BLOCK-04: Table ───────────────────────────────────────────────────
    // FIX: <colgroup> ile sütun genişlikleri güvenilir biçimde uygulanır.

    private static string GenerateTable(BlockDto block)
    {
        var cfg = Deserialize<TableConfig>(block.Config);
        var sb = new StringBuilder();
        var headerBg = cfg.HeaderBackgroundColor ?? "#E0E0E0";
        var altColor = cfg.AlternateRowColor ?? "#F9F9F9";

        sb.AppendLine("    <table class=\"dt\">");

        // Colgroup — table-layout:fixed ile sütun genişliklerinin uygulanması için zorunlu
        bool hasWidths = cfg.Columns.Any(c => !string.IsNullOrEmpty(c.Width));
        if (hasWidths)
        {
            sb.AppendLine("      <colgroup>");
            foreach (var col in cfg.Columns)
            {
                var colStyle = !string.IsNullOrEmpty(col.Width)
                    ? $" style=\"width:{XmlEscape(col.Width)}\""
                    : string.Empty;
                sb.AppendLine($"        <col{colStyle}/>");
            }
            sb.AppendLine("      </colgroup>");
        }

        if (cfg.ShowHeader)
        {
            sb.AppendLine($"      <thead><tr style=\"background:{XmlEscape(headerBg)}\">");
            foreach (var col in cfg.Columns)
                sb.AppendLine($"        <th>{XmlEscape(col.Header)}</th>");
            sb.AppendLine("      </tr></thead>");
        }

        sb.AppendLine("      <tbody>");
        sb.AppendLine($"        <xsl:for-each select=\"{XmlAttr(cfg.IterateOver)}\">");
        sb.AppendLine("          <xsl:variable name=\"pos\" select=\"position()\"/>");
        sb.AppendLine($"          <tr><xsl:if test=\"$pos mod 2 = 0\"><xsl:attribute name=\"style\">background:{XmlEscape(altColor)}</xsl:attribute></xsl:if>");
        foreach (var col in cfg.Columns)
            sb.AppendLine($"            <td>{FormatCell(col.Xpath, col.Format)}</td>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </xsl:for-each>");
        sb.AppendLine("      </tbody>");
        sb.Append("    </table>");
        return sb.ToString();
    }

    // ── BLOCK-05: ForEach ─────────────────────────────────────────────────

    private string GenerateForEach(BlockDto block, BlockTreeDto tree)
    {
        var cfg = Deserialize<ForEachConfig>(block.Config);
        var sb = new StringBuilder();
        sb.AppendLine($"    <xsl:for-each select=\"{XmlAttr(cfg.IterateOver)}\">");

        foreach (var childId in cfg.Children)
        {
            if (!tree.Blocks.TryGetValue(childId, out var child)) continue;
            sb.AppendLine("      " + DispatchBlock(child, tree));
        }

        sb.Append("    </xsl:for-each>");
        return sb.ToString();
    }

    // ── BLOCK-06: Conditional ─────────────────────────────────────────────

    private string GenerateConditional(BlockDto block, BlockTreeDto tree)
    {
        var cfg = Deserialize<ConditionalConfig>(block.Config);
        var test = BuildXPathTest(cfg.Condition);
        var sb = new StringBuilder();

        if (cfg.ElseBlockIds.Count == 0)
        {
            sb.AppendLine($"    <xsl:if test=\"{XmlAttr(test)}\">");
            foreach (var id in cfg.ThenBlockIds)
            {
                if (!tree.Blocks.TryGetValue(id, out var child)) continue;
                sb.AppendLine("      " + DispatchBlock(child, tree));
            }
            sb.Append("    </xsl:if>");
        }
        else
        {
            sb.AppendLine("    <xsl:choose>");
            sb.AppendLine($"      <xsl:when test=\"{XmlAttr(test)}\">");
            foreach (var id in cfg.ThenBlockIds)
            {
                if (!tree.Blocks.TryGetValue(id, out var child)) continue;
                sb.AppendLine("        " + GenerateBlock(child, tree));
            }
            sb.AppendLine("      </xsl:when>");
            sb.AppendLine("      <xsl:otherwise>");
            foreach (var id in cfg.ElseBlockIds)
            {
                if (!tree.Blocks.TryGetValue(id, out var child)) continue;
                sb.AppendLine("        " + GenerateBlock(child, tree));
            }
            sb.AppendLine("      </xsl:otherwise>");
            sb.Append("    </xsl:choose>");
        }

        return sb.ToString();
    }

    private static string BuildXPathTest(ConditionalCondition cond)
    {
        var xpath = cond.Xpath;
        var val = cond.Value ?? string.Empty;

        return cond.Operator switch
        {
            "equals"      => $"{xpath} = '{val}'",
            "notEquals"   => $"{xpath} != '{val}'",
            "contains"    => $"contains({xpath}, '{val}')",
            "greaterThan" => $"number({xpath}) > {val}",
            "lessThan"    => $"number({xpath}) < {val}",
            "exists"      => xpath,
            "notExists"   => $"not({xpath})",
            _             => xpath
        };
    }

    // ── BLOCK-07: Image ───────────────────────────────────────────────────

    private static string GenerateImage(BlockDto block)
    {
        var cfg = Deserialize<ImageConfig>(block.Config);
        var alignStyle = cfg.Alignment switch
        {
            "center" => "text-align:center",
            "right"  => "text-align:right",
            _        => "text-align:left"
        };
        var widthAttr  = !string.IsNullOrEmpty(cfg.Width)  ? $" width=\"{XmlEscape(cfg.Width)}\"" : string.Empty;
        var heightAttr = !string.IsNullOrEmpty(cfg.Height) ? $" height=\"{XmlEscape(cfg.Height)}\"" : string.Empty;
        var alt        = XmlEscape(cfg.AltText ?? cfg.AssetType);
        var srcAttr    = !string.IsNullOrEmpty(cfg.AssetId)
            ? $" src=\"/api/assets/{XmlAttr(cfg.AssetId)}/serve\""
            : string.Empty;

        return $"    <div style=\"{alignStyle}\"><img{srcAttr}{widthAttr}{heightAttr} alt=\"{alt}\"/></div>";
    }

    // ── BLOCK-08: DocumentInfo ────────────────────────────────────────────

    private static string GenerateDocumentInfo(BlockDto block)
    {
        var cfg = Deserialize<DocumentInfoConfig>(block.Config);
        var tableClass = (cfg.Bordered ?? true) ? "di" : "di-plain";
        var sb = new StringBuilder();
        sb.AppendLine($"    <table class=\"{tableClass}\">");
        sb.AppendLine("      <tbody>");
        foreach (var row in cfg.Rows)
        {
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td>{XmlEscape(row.Label)}</td>");
            sb.AppendLine($"          <td><xsl:value-of select=\"{XmlAttr(row.Xpath)}\"/></td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.Append("    </table>");
        return sb.ToString();
    }

    // ── BLOCK-09: Totals ──────────────────────────────────────────────────
    // FIX: Her hücreye explicit hizalama — table element'ine text-align vermek işe yaramıyor.

    private static string GenerateTotals(BlockDto block)
    {
        var cfg = Deserialize<TotalsConfig>(block.Config);
        var lblWidthStyle = !string.IsNullOrEmpty(cfg.LabelWidth)
            ? $" style=\"width:{XmlEscape(cfg.LabelWidth)}\""
            : string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine("    <table class=\"tot\">");
        sb.AppendLine("      <tbody>");
        foreach (var row in cfg.Rows)
        {
            var hlClass = row.Highlight ? " class=\"hl\"" : string.Empty;
            sb.AppendLine($"        <tr{hlClass}>");
            sb.AppendLine($"          <td class=\"lbl\"{lblWidthStyle}><strong>{XmlEscape(row.Label)}</strong></td>");
            sb.AppendLine($"          <td class=\"val\"><xsl:value-of select=\"{XmlAttr(row.Xpath)}\"/></td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.Append("    </table>");
        return sb.ToString();
    }

    // ── BLOCK-10: Notes ───────────────────────────────────────────────────

    private static string GenerateNotes(BlockDto block)
    {
        var cfg = Deserialize<NotesConfig>(block.Config);
        var prefix = XmlEscape(cfg.Prefix ?? string.Empty);
        return
            $"""
                <div class="notes">
                  <xsl:for-each select="{XmlAttr(cfg.IterateOver)}">
                    <p>{prefix}<xsl:value-of select="."/></p>
                  </xsl:for-each>
                </div>
            """;
    }

    // ── BLOCK-11: BankInfo ────────────────────────────────────────────────

    private static string GenerateBankInfo(BlockDto block)
    {
        var cfg = Deserialize<BankInfoConfig>(block.Config);
        var sb = new StringBuilder("    <div class=\"bank-info\">");

        if (!string.IsNullOrEmpty(cfg.BankNameXpath))
            sb.Append($"<p><strong>Banka:</strong> <xsl:value-of select=\"{XmlAttr(cfg.BankNameXpath)}\"/></p>");
        if (!string.IsNullOrEmpty(cfg.IbanXpath))
            sb.Append($"<p><strong>IBAN:</strong> <xsl:value-of select=\"{XmlAttr(cfg.IbanXpath)}\"/></p>");
        if (!string.IsNullOrEmpty(cfg.PaymentTermsXpath))
            sb.Append($"<p><xsl:value-of select=\"{XmlAttr(cfg.PaymentTermsXpath)}\"/></p>");

        sb.Append("</div>");
        return sb.ToString();
    }

    // ── BLOCK-12: ETTN ────────────────────────────────────────────────────
    // FIX: Gerçek QR kod üretimi; UUID gizli div'de saklanır, qrcode.js ile görüntülenir.

    private static string GenerateETTN(BlockDto block)
    {
        var cfg = Deserialize<EttnConfig>(block.Config);

        // HTML element ID'lerinde tire kullanılamaz — sadece alfanümerik
        var safeId = Regex.Replace(block.Id, "[^a-zA-Z0-9]", "");

        var sb = new StringBuilder();
        sb.AppendLine("    <div class=\"ettn\">");

        if (cfg.ShowEttn)
            sb.AppendLine($"      <p><strong>ETTN:</strong> <xsl:value-of select=\"{XmlAttr(cfg.EttnXpath)}\"/></p>");

        if (cfg.ShowQR)
        {
            var qrWidth  = cfg.QrWidth  > 0 ? cfg.QrWidth  : 80;
            var qrHeight = cfg.QrHeight > 0 ? cfg.QrHeight : 80;
            var flexJustify = QrFlexJustify(cfg.QrAlignment);

            // UUID gizli — sadece JS tarafından okunur
            sb.AppendLine($"      <div id=\"qrv{safeId}\" style=\"display:none;\"><xsl:value-of select=\"{XmlAttr(cfg.EttnXpath)}\"/></div>");

            // QR kod container — flex ile hizalama (qrcode.js canvas'a display:block ekler, text-align çalışmaz)
            sb.AppendLine($"      <div style=\"display:flex; justify-content:{flexJustify};\">");
            sb.AppendLine($"        <div id=\"qrc{safeId}\" style=\"line-height:0; flex-shrink:0;\"></div>");
            sb.AppendLine("      </div>");

            // window.onload ile QR oluştur — qrcode.js head'den yükleniyor
            sb.AppendLine("      <script type=\"text/javascript\">");
            sb.AppendLine("        window.addEventListener('load', function() {");
            sb.AppendLine($"          var v = document.getElementById('qrv{safeId}');");
            sb.AppendLine($"          var c = document.getElementById('qrc{safeId}');");
            sb.AppendLine("          if (!v || !c || typeof QRCode === 'undefined') return;");
            sb.AppendLine("          var t = v.textContent ? v.textContent.trim() : '';");
            sb.AppendLine("          if (!t) return;");
            sb.AppendLine($"          new QRCode(c, {{ text: t, width: {qrWidth}, height: {qrHeight}, correctLevel: QRCode.CorrectLevel.M }});");
            sb.AppendLine("        });");
            sb.AppendLine("      </script>");
        }

        sb.Append("    </div>");
        return sb.ToString();
    }

    // ── BLOCK-18: GibKarekod ─────────────────────────────────────────────
    // GİB standart e-Fatura karekod içeriği — XPath'ler sabittir.

    private static string GenerateGibKarekod(BlockDto block)
    {
        var cfg = Deserialize<GibKarekodConfig>(block.Config);
        var safeId = Regex.Replace(block.Id, "[^a-zA-Z0-9]", "");
        var qrWidth  = cfg.QrWidth  > 0 ? cfg.QrWidth  : 150;
        var qrHeight = cfg.QrHeight > 0 ? cfg.QrHeight : 150;
        var flexJustify = QrFlexJustify(cfg.QrAlignment);

        var sb = new StringBuilder();
        sb.AppendLine("    <div class=\"ettn\">");

        // Hidden div — GİB JSON payload
        sb.AppendLine($"      <div id=\"qrv{safeId}\" style=\"display:none;\">");
        sb.AppendLine("        {\"vkntckn\":\"<xsl:value-of select=\"n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']\"/>\",");
        sb.AppendLine("        \"avkntckn\":\"<xsl:value-of select=\"n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']\"/>\",");
        sb.AppendLine("        <xsl:if test=\"//n1:Invoice/cbc:ProfileID = 'YOLCUBERABERFATURA'\">\"pasaportno\":\"<xsl:value-of select=\"n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:Person/cac:IdentityDocumentReference/cbc:ID\"/>\",</xsl:if>");
        sb.AppendLine("        <xsl:if test=\"//n1:Invoice/cbc:ProfileID = 'YOLCUBERABERFATURA'\">\"aracikurumvkn\":\"<xsl:value-of select=\"n1:Invoice/cac:TaxRepresentativeParty/cac:PartyIdentification/cbc:ID[@schemeID='ARACIKURUMVKN']\"/>\",</xsl:if>");
        sb.AppendLine("        \"senaryo\":\"<xsl:value-of select=\"n1:Invoice/cbc:ProfileID\"/>\",");
        sb.AppendLine("        \"tip\":\"<xsl:value-of select=\"n1:Invoice/cbc:InvoiceTypeCode\"/>\",");
        sb.AppendLine("        \"tarih\":\"<xsl:value-of select=\"n1:Invoice/cbc:IssueDate\"/>\",");
        sb.AppendLine("        \"no\":\"<xsl:value-of select=\"n1:Invoice/cbc:ID\"/>\",");
        sb.AppendLine("        \"ettn\":\"<xsl:value-of select=\"n1:Invoice/cbc:UUID\"/>\",");
        sb.AppendLine("        \"parabirimi\":\"<xsl:value-of select=\"n1:Invoice/cbc:DocumentCurrencyCode\"/>\",");
        sb.AppendLine("        \"malhizmettoplam\":\"<xsl:value-of select=\"n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount\"/>\",");
        sb.AppendLine("        <xsl:for-each select=\"n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']\">");
        sb.AppendLine("        \"kdvmatrah(<xsl:value-of select=\"cbc:Percent\"/>)\":\"<xsl:value-of select=\"cbc:TaxableAmount\"/>\",");
        sb.AppendLine("        </xsl:for-each>");
        sb.AppendLine("        <xsl:for-each select=\"n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']\">");
        sb.AppendLine("        \"hesaplanankdv(<xsl:value-of select=\"cbc:Percent\"/>)\":\"<xsl:value-of select=\"cbc:TaxAmount\"/>\",");
        sb.AppendLine("        </xsl:for-each>");
        sb.AppendLine("        \"vergidahil\":\"<xsl:value-of select=\"n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount\"/>\",");
        sb.AppendLine("        \"odenecek\":\"<xsl:value-of select=\"n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount\"/>\"");
        sb.AppendLine("        }");
        sb.AppendLine("      </div>");

        // QR container — flex alignment
        sb.AppendLine($"      <div style=\"display:flex; justify-content:{flexJustify};\">");
        sb.AppendLine($"        <div id=\"qrc{safeId}\" style=\"line-height:0; flex-shrink:0;\"></div>");
        sb.AppendLine("      </div>");

        // QR oluşturma scripti
        sb.AppendLine("      <script type=\"text/javascript\">");
        sb.AppendLine("        window.addEventListener('load', function() {");
        sb.AppendLine($"          var v = document.getElementById('qrv{safeId}');");
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

    // ── BLOCK-15: Variable ────────────────────────────────────────────────
    // Görünür HTML çıktısı üretmez; XSLT değişkeni tanımlar.

    private static string GenerateVariable(BlockDto block)
    {
        var cfg = Deserialize<VariableConfig>(block.Config);
        if (string.IsNullOrWhiteSpace(cfg.Name))
            return "    <!-- Variable: 'name' alanı zorunlu -->";
        return $"    <xsl:variable name=\"{XmlAttr(cfg.Name)}\" select=\"{XmlAttr(cfg.Xpath)}\"/>";
    }

    // ── BLOCK-16: ConditionalText ─────────────────────────────────────────
    // Koşula göre farklı metin veya XPath değeri gösterir.

    private static string GenerateConditionalText(BlockDto block)
    {
        var cfg = Deserialize<ConditionalTextConfig>(block.Config);
        var test = BuildXPathTest(cfg.Condition);

        var thenPart = cfg.ThenIsStatic
            ? $"<xsl:text>{XmlEscape(cfg.ThenContent)}</xsl:text>"
            : $"<xsl:value-of select=\"{XmlAttr(cfg.ThenContent)}\"/>";

        var elsePart = cfg.ElseIsStatic
            ? $"<xsl:text>{XmlEscape(cfg.ElseContent)}</xsl:text>"
            : $"<xsl:value-of select=\"{XmlAttr(cfg.ElseContent)}\"/>";

        return $"    <span><xsl:choose>" +
               $"<xsl:when test=\"{XmlAttr(test)}\">{thenPart}</xsl:when>" +
               $"<xsl:otherwise>{elsePart}</xsl:otherwise>" +
               $"</xsl:choose></span>";
    }

    // ── BLOCK-17: TaxSummary ──────────────────────────────────────────────
    // Türk e-Fatura KDV özet tablosu.

    private static string GenerateTaxSummary(BlockDto block)
    {
        var cfg = Deserialize<TaxSummaryConfig>(block.Config);
        var headerBg = cfg.HeaderBackgroundColor ?? "#E0E0E0";
        var sb = new StringBuilder();

        sb.AppendLine("    <table class=\"dt\">");
        sb.AppendLine($"      <thead><tr style=\"background:{XmlEscape(headerBg)}\">");
        if (cfg.ShowPercent)
            sb.AppendLine("        <th>KDV Oranı</th>");
        sb.AppendLine("        <th>Matrah</th>");
        sb.AppendLine("        <th>KDV Tutarı</th>");
        sb.AppendLine("      </tr></thead>");
        sb.AppendLine("      <tbody>");
        sb.AppendLine($"        <xsl:for-each select=\"{XmlAttr(cfg.TaxTotalXpath)}\">");
        sb.AppendLine("          <tr>");
        if (cfg.ShowPercent)
            sb.AppendLine($"            <td><xsl:value-of select=\"{XmlAttr(cfg.PercentXpath)}\"/>%</td>");
        sb.AppendLine($"            <td>{FormatCell(cfg.TaxableAmountXpath, "currency")}</td>");
        sb.AppendLine($"            <td>{FormatCell(cfg.TaxAmountXpath, "currency")}</td>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </xsl:for-each>");
        sb.AppendLine("      </tbody>");
        sb.Append("    </table>");
        return sb.ToString();
    }

    // ── BLOCK-13: Divider ─────────────────────────────────────────────────

    private static string GenerateDivider(BlockDto block)
    {
        var cfg = Deserialize<DividerConfig>(block.Config);
        var style = new StringBuilder();
        style.Append($"border-top-style:{XmlEscape(cfg.Style)};");
        if (!string.IsNullOrEmpty(cfg.Color))        style.Append($"border-color:{XmlEscape(cfg.Color)};");
        if (!string.IsNullOrEmpty(cfg.Thickness))    style.Append($"border-top-width:{XmlEscape(cfg.Thickness)};");
        if (!string.IsNullOrEmpty(cfg.MarginTop))    style.Append($"margin-top:{XmlEscape(cfg.MarginTop)};");
        if (!string.IsNullOrEmpty(cfg.MarginBottom)) style.Append($"margin-bottom:{XmlEscape(cfg.MarginBottom)};");
        return $"    <hr style=\"{style}\"/>";
    }

    // ── BLOCK-14: Spacer ──────────────────────────────────────────────────

    private static string GenerateSpacer(BlockDto block)
    {
        var cfg = Deserialize<SpacerConfig>(block.Config);
        return $"    <div style=\"height:{XmlEscape(cfg.Height)}\">&#160;</div>";
    }

    // ── Validation ────────────────────────────────────────────────────────

    private static string? Validate(string xslt)
    {
        try
        {
            var transform = new XslCompiledTransform();
            using var reader = XmlReader.Create(new StringReader(xslt));
            transform.Load(reader);
            return null;
        }
        catch (XsltException ex)
        {
            return $"XSLT doğrulama hatası (satır {ex.LineNumber}): {ex.Message}";
        }
        catch (XmlException ex)
        {
            return $"XML ayrıştırma hatası (satır {ex.LineNumber}): {ex.Message}";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>CSS flex justify-content değeri — QR hizalaması için.</summary>
    private static string QrFlexJustify(string? alignment) => alignment switch
    {
        "center" => "center",
        "left"   => "flex-start",
        _        => "flex-end"
    };

    /// <summary>Inline style attribute from text style properties (returns empty string if no styles).</summary>
    private static string BuildStyleAttr(string? fontWeight, string? fontStyle, string? fontSize, string? color)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(fontWeight) && fontWeight != "normal")
            parts.Add($"font-weight:{XmlEscape(fontWeight)}");
        if (!string.IsNullOrEmpty(fontStyle) && fontStyle != "normal")
            parts.Add($"font-style:{XmlEscape(fontStyle)}");
        if (!string.IsNullOrEmpty(fontSize))
            parts.Add($"font-size:{XmlEscape(fontSize)}");
        if (!string.IsNullOrEmpty(color))
            parts.Add($"color:{XmlEscape(color)}");
        return parts.Count > 0 ? $" style=\"{string.Join(";", parts)}\"" : string.Empty;
    }

    /// <summary>Generates XSLT cell content for a table column, applying numeric formatting when needed.</summary>
    private static string FormatCell(string xpath, string? format)
    {
        return format switch
        {
            "currency" =>
                $"<xsl:choose>" +
                $"<xsl:when test=\"number({XmlAttr(xpath)}) = number({XmlAttr(xpath)})\">" +
                $"<xsl:value-of select=\"format-number({XmlAttr(xpath)}, '#,##0.00')\"/>" +
                $"</xsl:when>" +
                $"<xsl:otherwise><xsl:value-of select=\"{XmlAttr(xpath)}\"/></xsl:otherwise>" +
                $"</xsl:choose>",
            "number" =>
                $"<xsl:choose>" +
                $"<xsl:when test=\"number({XmlAttr(xpath)}) = number({XmlAttr(xpath)})\">" +
                $"<xsl:value-of select=\"format-number({XmlAttr(xpath)}, '#,##0.##')\"/>" +
                $"</xsl:when>" +
                $"<xsl:otherwise><xsl:value-of select=\"{XmlAttr(xpath)}\"/></xsl:otherwise>" +
                $"</xsl:choose>",
            _ => $"<xsl:value-of select=\"{XmlAttr(xpath)}\"/>"
        };
    }

    private static T Deserialize<T>(JsonElement element) where T : new()
    {
        return element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null
            ? new T()
            : JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOpts) ?? new T();
    }

    /// <summary>XML element içeriği için güvenli escape.</summary>
    private static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>XML attribute değeri için güvenli escape (çift tırnak dahil).</summary>
    private static string XmlAttr(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;");
    }
}
