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

    private const string GibLogoBase64 = "data:image/png;base64,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";

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
              <xsl:decimal-format name="tr" decimal-separator="," grouping-separator="."/>

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
            "InvoiceTotals"   => GenerateInvoiceTotals(block),
            "GibLogo"         => GenerateGibLogo(block),
            "PartyInfo"       => GeneratePartyInfo(block),
            "InvoiceLineTable"=> GenerateInvoiceLineTable(block),
            "InvoiceHeader"   => GenerateInvoiceHeader(block),
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
        var bordered = cfg.Bordered == true;
        var borderColor = XmlEscape(string.IsNullOrWhiteSpace(cfg.BorderColor) ? "#555555" : cfg.BorderColor);
        var fontSize = string.IsNullOrWhiteSpace(cfg.FontSize) ? "inherit" : XmlEscape(cfg.FontSize);
        var outerStyle = bordered
            ? $" style=\"border:1px solid {borderColor};padding:4px 8px;\""
            : string.Empty;
        var pStyle = $"margin:0;line-height:1.6;font-size:{fontSize}";

        var staticBefore = cfg.StaticPosition != "after";
        var sb = new StringBuilder();
        sb.AppendLine($"    <div{outerStyle}>");

        // Sabit notlar — pozisyona göre
        var staticSnippet = new StringBuilder();
        foreach (var line in cfg.StaticLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            staticSnippet.AppendLine($"      <p style=\"{pStyle}\"><strong>{prefix}</strong>{XmlEscape(line)}</p>");
        }

        if (staticBefore)
            sb.Append(staticSnippet);

        // XPath notları
        if (!string.IsNullOrWhiteSpace(cfg.IterateOver))
        {
            sb.AppendLine($"      <xsl:for-each select=\"{XmlAttr(cfg.IterateOver)}\">");
            sb.AppendLine($"        <p style=\"{pStyle}\"><strong>{prefix}</strong><xsl:value-of select=\".\"/></p>");
            sb.AppendLine("      </xsl:for-each>");
        }

        if (!staticBefore)
            sb.Append(staticSnippet);

        sb.Append("    </div>");
        return sb.ToString();
    }

    // ── BLOCK-11: BankInfo ────────────────────────────────────────────────

    private static string GenerateBankInfo(BlockDto block)
    {
        var cfg = Deserialize<BankInfoConfig>(block.Config);
        var bankName = XmlEscape(cfg.BankName ?? string.Empty);
        var iban = XmlEscape(cfg.Iban ?? string.Empty);
        var ibanLabel = XmlEscape(cfg.IbanLabel ?? "IBAN: ");
        var bordered = cfg.Bordered == true;
        var borderColor = XmlEscape(string.IsNullOrWhiteSpace(cfg.BorderColor) ? "#555555" : cfg.BorderColor);
        var tableStyle = bordered
            ? $"width:100%;border-collapse:collapse;border:1px solid {borderColor};"
            : "width:100%;border-collapse:collapse;";
        return
            $"""
                <table style="{tableStyle}">
                  <tr>
                    <td style="padding:4px 8px;font-weight:bold;">{bankName}</td>
                    <td style="padding:4px 8px;text-align:right;">{ibanLabel}{iban}</td>
                  </tr>
                </table>
            """;
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

    // ── BLOCK: PartyInfo ─────────────────────────────────────────────────

    private static string GeneratePartyInfo(BlockDto block)
    {
        var cfg = Deserialize<PartyInfoConfig>(block.Config);
        var partyRoot = cfg.PartyType switch
        {
            "CustomerParty"         => "//cac:AccountingCustomerParty/cac:Party",
            "DespatchSupplierParty" => "//cac:DespatchSupplierParty/cac:Party",
            "DeliveryCustomerParty" => "//cac:DeliveryCustomerParty/cac:Party",
            "BuyerCustomerParty"    => "//cac:BuyerCustomerParty/cac:Party",
            _                       => "//cac:AccountingSupplierParty/cac:Party",
        };

        var fields = (cfg.Fields ?? new List<PartyInfoField>())
            .Where(f => f.Visible)
            .OrderBy(f => f.Order)
            .ToList();

        var fontSize  = string.IsNullOrWhiteSpace(cfg.FontSize) ? "9.5pt" : XmlEscape(cfg.FontSize);
        var outerStyle = cfg.Bordered
            ? " style=\"border:1px solid #555555;padding:4px 8px;\""
            : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"    <div{outerStyle}>");

        if (cfg.ShowTitle && !string.IsNullOrWhiteSpace(cfg.Title))
            sb.AppendLine($"      <p style=\"font-weight:bold;margin:0 0 4px 0;font-size:{fontSize}\">{XmlEscape(cfg.Title)}</p>");

        sb.AppendLine($"      <xsl:for-each select=\"{XmlAttr(partyRoot)}\">");

        if (cfg.LabelStyle == "inline")
        {
            foreach (var f in fields)
            {
                var label = XmlEscape(f.Label ?? string.Empty);
                var xpath = XmlAttr(f.RelativeXpath ?? string.Empty);
                var labelPart = string.IsNullOrWhiteSpace(f.Label) ? string.Empty : $"<strong>{label}: </strong>";
                sb.AppendLine($"        <p style=\"margin:0;font-size:{fontSize}\">{labelPart}<xsl:value-of select=\"{xpath}\"/></p>");
            }
        }
        else // table (default)
        {
            sb.AppendLine($"        <table style=\"width:100%;border-collapse:collapse;font-size:{fontSize}\">");
            foreach (var f in fields)
            {
                var label = XmlEscape(f.Label ?? string.Empty);
                var xpath = XmlAttr(f.RelativeXpath ?? string.Empty);
                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td style=\"font-weight:bold;padding:1px 4px;white-space:nowrap;width:35%;vertical-align:top\">{label}</td>");
                sb.AppendLine($"            <td style=\"padding:1px 4px\"><xsl:value-of select=\"{xpath}\"/></td>");
                sb.AppendLine("          </tr>");
            }
            sb.AppendLine("        </table>");
        }

        sb.AppendLine("      </xsl:for-each>");
        sb.AppendLine("    </div>");
        return sb.ToString();
    }

    // ── BLOCK: InvoiceLineTable ───────────────────────────────────────────

    private static string GenerateInvoiceLineTable(BlockDto block)
    {
        var cfg = Deserialize<InvoiceLineTableConfig>(block.Config);
        var columns = (cfg.Columns ?? new List<InvoiceLineColumn>())
            .Where(c => c.Visible)
            .OrderBy(c => c.Order)
            .ToList();

        var fontSize    = string.IsNullOrWhiteSpace(cfg.FontSize) ? "9pt" : XmlEscape(cfg.FontSize);
        var headerBg    = string.IsNullOrWhiteSpace(cfg.HeaderBackgroundColor) ? "#E0E0E0" : XmlEscape(cfg.HeaderBackgroundColor);
        var altRowColor = string.IsNullOrWhiteSpace(cfg.AlternateRowColor)    ? "#F9F9F9" : XmlEscape(cfg.AlternateRowColor);
        var borderAttr  = cfg.Bordered ? "1px solid #555555" : "none";

        var sb = new StringBuilder();

        if (cfg.ShowTitle && !string.IsNullOrWhiteSpace(cfg.Title))
            sb.AppendLine($"    <p style=\"font-weight:bold;margin:0 0 4px 0;font-size:{fontSize}\">{XmlEscape(cfg.Title)}</p>");

        sb.AppendLine($"    <table style=\"width:100%;border-collapse:collapse;font-size:{fontSize}\">");

        if (cfg.ShowHeader)
        {
            sb.AppendLine($"      <thead><tr style=\"background:{headerBg}\">");
            if (cfg.ShowRowNumber)
                sb.AppendLine($"        <th style=\"border:{borderAttr};padding:2px 4px;text-align:center;width:4%\">#</th>");
            foreach (var col in columns)
            {
                var header = XmlEscape(col.Header ?? string.Empty);
                var widthAttr = string.IsNullOrWhiteSpace(col.Width) ? string.Empty : $" width=\"{XmlEscape(col.Width)}\"";
                sb.AppendLine($"        <th style=\"border:{borderAttr};padding:2px 4px;text-align:center\"{widthAttr}>{header}</th>");
            }
            sb.AppendLine("      </tr></thead>");
        }

        sb.AppendLine("      <tbody>");
        sb.AppendLine($"      <xsl:for-each select=\"{XmlAttr(cfg.IterateOver)}\">");
        sb.AppendLine("        <xsl:variable name=\"rowBg\">");
        sb.AppendLine("          <xsl:choose>");
        sb.AppendLine($"            <xsl:when test=\"position() mod 2 = 0\">{altRowColor}</xsl:when>");
        sb.AppendLine("            <xsl:otherwise>#ffffff</xsl:otherwise>");
        sb.AppendLine("          </xsl:choose>");
        sb.AppendLine("        </xsl:variable>");
        sb.AppendLine("        <tr style=\"background:{$rowBg}\">");

        if (cfg.ShowRowNumber)
            sb.AppendLine($"          <td style=\"border:{borderAttr};padding:2px 4px;text-align:center\"><xsl:value-of select=\"position()\"/></td>");

        foreach (var col in columns)
        {
            var xpathAttr   = XmlAttr(col.RelativeXpath ?? string.Empty);
            var cellContent = GetColumnCellContent(col.Format ?? "text", xpathAttr);
            var align = col.Format is "currency" or "number" or "percent" or "percentDirect" or "quantityWithUnit"
                ? "text-align:right;" : string.Empty;
            sb.AppendLine($"          <td style=\"border:{borderAttr};padding:2px 4px;{align}\">{cellContent}</td>");
        }

        sb.AppendLine("        </tr>");
        sb.AppendLine("      </xsl:for-each>");
        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");
        return sb.ToString();
    }

    private static string GetColumnCellContent(string format, string xpathAttr)
    {
        return format switch
        {
            "currency" =>
                $"<xsl:if test=\"number({xpathAttr}) = number({xpathAttr})\">" +
                $"<xsl:value-of select=\"format-number(number({xpathAttr}),'#.##0,00','tr')\"/>" +
                "</xsl:if>",
            "number" =>
                $"<xsl:if test=\"number({xpathAttr}) = number({xpathAttr})\">" +
                $"<xsl:value-of select=\"format-number(number({xpathAttr}),'#.##0,###','tr')\"/>" +
                "</xsl:if>",
            "percent" =>
                $"<xsl:if test=\"number({xpathAttr}) = number({xpathAttr})\">" +
                $"<xsl:value-of select=\"format-number(number({xpathAttr})*100,'#.##0,###','tr')\"/>%" +
                "</xsl:if>",
            "percentDirect" =>
                $"<xsl:if test=\"string-length({xpathAttr}) &gt; 0\">" +
                $"<xsl:value-of select=\"{xpathAttr}\"/>%" +
                "</xsl:if>",
            "quantityWithUnit" =>
                $"<xsl:if test=\"number({xpathAttr}) = number({xpathAttr})\">" +
                $"<xsl:value-of select=\"format-number(number({xpathAttr}),'#.##0,###','tr')\"/>" +
                "</xsl:if>" +
                "&#160;" +
                BuildUnitCodeChoose($"{xpathAttr}/@unitCode"),
            _ =>
                $"<xsl:value-of select=\"{xpathAttr}\"/>",
        };
    }

    private static string BuildUnitCodeChoose(string unitCodeXpath)
    {
        // GİB standart birim kodları — Türkçe karşılıkları
        var codes = new (string Code, string Label)[]
        {
            ("C62", "Adet"),
            ("BX",  "Kutu"),
            ("TNE", "ton"),
            ("LTR", "lt"),
            ("TN",  "Teneke"),
            ("KGM", "kg"),
            ("KJO", "kJ"),
            ("GRM", "g"),
            ("MGM", "mg"),
            ("NT",  "Net Ton"),
            ("GT",  "Gross Ton"),
            ("MTR", "m"),
            ("MMT", "mm"),
            ("KTM", "km"),
            ("MLT", "ml"),
            ("MMQ", "mm3"),
            ("CLT", "cl"),
            ("CMK", "cm2"),
            ("CMQ", "cm3"),
            ("CMT", "cm"),
            ("MTK", "m2"),
            ("MTQ", "m3"),
            ("DAY", "Gün"),
            ("MON", "Ay"),
            ("PA",  "Paket"),
            ("KWH", "KWH"),
            ("ANN", "Yıl"),
            ("HUR", "Saat"),
            ("D61", "Dakika"),
            ("D62", "Saniye"),
            ("CCT", "Ton baş.taşıma kap."),
            ("D30", "Brüt kalori"),
            ("D40", "1000 lt"),
            ("LPA", "saf alkol lt"),
            ("B32", "kg.m2"),
            ("NCL", "hücre adet"),
            ("PR",  "Çift"),
            ("R9",  "1000 m3"),
            ("SET", "Set"),
            ("T3",  "1000 adet"),
            ("PK",  "Koli"),
            ("CR",  "Kasa/Sandık"),
            ("BG",  "Poşet/Torba"),
            ("GFI", "Fıssıle İzotop Gramı"),
            ("CEN", "Yüz Adet"),
            ("KPO", "Kilogram Potasyum Oksit"),
            ("MND", "Kurutulmuş Net Ağırlıklı Kilogramı"),
            ("3I",  "Kilogram-Adet"),
            ("KFO", "Difosfor Pentaoksit Kilogramı"),
            ("KHY", "Hidrojen Peroksik Kilogramı"),
            ("KMA", "Metil Aminlerin Kilogramı"),
            ("KNI", "Azotun Kilogramı"),
            ("KPH", "Kilogram Potasyum Hidroksit"),
            ("KSD", "%90 Kuru Ürün Kilogramı"),
            ("KSH", "Sodyum Hidroksit Kilogramı"),
            ("KUR", "Uranyum Kilogramı"),
            ("D32", "Terawatt Saat"),
            ("GWH", "Gigawatt Saat"),
            ("MWH", "Megawatt Saat (1000 kW.h)"),
            ("KWT", "Kilowatt"),
            ("DMK", "Desimetre Kare"),
            ("CTM", "Karat"),
            ("SM3", "Standart Metreküp"),
            ("CT",  "Karton"),
            ("DMT", "Desimetre"),
            ("DMQ", "Desimetre Küp"),
            ("KTN", "Kiloton"),
            ("D93", "Doz"),
            ("LM",  "Metre Tül"),
            ("BO",  "Şişe"),
            ("H80", "Rack Unit"),
            ("RA",  "Rack"),
            ("TU",  "Tüp"),
            ("BLL", "Fıçı"),
            ("TC",  "Kamyon"),
            ("PG",  "Plaka"),
            ("DPC", "Düzüne"),
            ("LR",  "Tabaka"),
            ("JOU", "Vardiya"),
            ("DRL", "Rulo"),
            ("ACR", "Dönüm"),
            ("E53", "Test"),
            ("H82", "Puan"),
            ("SQR", "Ayak"),
            ("AYR", "Altın Ayarı"),
            ("BAS", "Bas"),
            ("CPR", "Adet-Çift"),
            ("GMS", "Gümüş"),
            ("H62", "Yüz Adet"),
            ("KHO", "Hidroje Peroksit Kilogramı"),
            ("KH6", "Kilogram-Baş"),
            ("KOH", "Kilogram Potasyum Hidroksit"),
            ("KPR", "Kilogram-Çift"),
            ("K20", "Kilogram Potasyum Oksit"),
            ("K58", "Kurutulmuş Net Ağırlıklı Kilogramı"),
            ("K62", "Kilogram-Adet"),
            ("NCR", "Karat"),
            ("OMV", "OTV Maktu Vergi"),
            ("OTB", "OTV Birim Fiyatı"),
            ("D63", "Cilt"),
        };

        var sb = new StringBuilder();
        sb.Append("<xsl:choose>");
        foreach (var (code, label) in codes)
            sb.Append($"<xsl:when test=\"{unitCodeXpath}='{code}'\">{label}</xsl:when>");
        sb.Append($"<xsl:otherwise><xsl:value-of select=\"{unitCodeXpath}\"/></xsl:otherwise>");
        sb.Append("</xsl:choose>");
        return sb.ToString();
    }

    // ── BLOCK: InvoiceHeader ──────────────────────────────────────────────

    private static string GenerateInvoiceHeader(BlockDto block)
    {
        var cfg = Deserialize<InvoiceHeaderConfig>(block.Config);
        var fields = (cfg.Fields ?? new List<InvoiceHeaderField>())
            .Where(f => f.Visible)
            .OrderBy(f => f.Order)
            .ToList();

        var fontSize   = string.IsNullOrWhiteSpace(cfg.FontSize) ? "9.5pt" : XmlEscape(cfg.FontSize);
        var outerStyle = cfg.Bordered
            ? " style=\"border:1px solid #555555;padding:4px 8px;\""
            : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"    <div{outerStyle}>");

        if (cfg.ShowTitle && !string.IsNullOrWhiteSpace(cfg.Title))
            sb.AppendLine($"      <p style=\"font-weight:bold;margin:0 0 4px 0;font-size:{fontSize}\">{XmlEscape(cfg.Title)}</p>");

        if (cfg.LabelStyle == "inline")
        {
            foreach (var f in fields)
            {
                var label = XmlEscape(f.Label ?? string.Empty);
                var xpath = XmlAttr(f.Xpath ?? string.Empty);
                var labelPart = string.IsNullOrWhiteSpace(f.Label) ? string.Empty : $"<strong>{label}: </strong>";
                sb.AppendLine($"      <p style=\"margin:0;font-size:{fontSize}\">{labelPart}<xsl:value-of select=\"{xpath}\"/></p>");
            }
        }
        else // table (default)
        {
            sb.AppendLine($"      <table style=\"width:100%;border-collapse:collapse;font-size:{fontSize}\">");
            foreach (var f in fields)
            {
                var label = XmlEscape(f.Label ?? string.Empty);
                var xpath = XmlAttr(f.Xpath ?? string.Empty);
                sb.AppendLine("        <tr>");
                sb.AppendLine($"          <td style=\"font-weight:bold;padding:1px 4px;white-space:nowrap;width:40%;vertical-align:top\">{label}</td>");
                sb.AppendLine($"          <td style=\"padding:1px 4px\"><xsl:value-of select=\"{xpath}\"/></td>");
                sb.AppendLine("        </tr>");
            }
            sb.AppendLine("      </table>");
        }

        sb.AppendLine("    </div>");
        return sb.ToString();
    }

    // ── BLOCK: InvoiceTotals ──────────────────────────────────────────────

    private static string GenerateInvoiceTotals(BlockDto block)
    {
        var cfg = Deserialize<InvoiceTotalsConfig>(block.Config);
        var fields = (cfg.Fields ?? new List<InvoiceTotalsField>())
            .Where(f => f.Visible)
            .OrderBy(f => f.Order)
            .ToList();

        if (fields.Count == 0)
            return "<!-- InvoiceTotals: görünür alan yok -->";

        var currencyXpath = XmlAttr(string.IsNullOrWhiteSpace(cfg.CurrencyXpath)
            ? "//cbc:DocumentCurrencyCode"
            : cfg.CurrencyXpath);

        var sb = new StringBuilder();
        sb.AppendLine("    <table class=\"tot\">");
        foreach (var f in fields)
        {
            var rowClass = f.Highlight ? " class=\"hl\"" : string.Empty;
            var labelEsc = XmlEscape(f.Label ?? string.Empty);
            var xpathAttr = XmlAttr(f.Xpath ?? string.Empty);
            var currencySuffix = cfg.ShowCurrency
                ? $" <xsl:value-of select=\"{currencyXpath}\"/>"
                : string.Empty;
            sb.AppendLine($"      <tr{rowClass}>");
            sb.AppendLine($"        <td class=\"lbl\">{labelEsc}</td>");
            sb.AppendLine($"        <td class=\"val\"><xsl:value-of select=\"format-number(number({xpathAttr}),'#.##0,00','tr')\"/>{currencySuffix}</td>");
            sb.AppendLine("      </tr>");
        }
        sb.AppendLine("    </table>");
        return sb.ToString();
    }

    // ── BLOCK: GibLogo ────────────────────────────────────────────────────

    private static string GenerateGibLogo(BlockDto block)
    {
        var cfg = Deserialize<GibLogoConfig>(block.Config);
        var width  = string.IsNullOrWhiteSpace(cfg.Width)  ? "80px" : XmlEscape(cfg.Width);
        var height = string.IsNullOrWhiteSpace(cfg.Height) ? "80px" : XmlEscape(cfg.Height);
        var marginStyle = cfg.Alignment switch
        {
            "left"  => "margin:0 auto 0 0",
            "right" => "margin:0 0 0 auto",
            _       => "margin:0 auto",
        };
        return $"    <img src=\"{GibLogoBase64}\" alt=\"GIB Logo\" style=\"display:block;width:{width};height:{height};object-fit:contain;{marginStyle};\"/>";
    }
}
