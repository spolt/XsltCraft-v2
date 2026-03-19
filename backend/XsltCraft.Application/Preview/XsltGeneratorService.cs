using System.Text;
using System.Text.Json;
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
            var xslt = WrapStylesheet(body);
            var validationError = Validate(xslt);
            return validationError is null ? (xslt, null) : (null, validationError);
        }
        catch (Exception ex)
        {
            return (null, $"XSLT üretim hatası: {ex.Message}");
        }
    }

    // ── Stylesheet wrapper ────────────────────────────────────────────────

    private static string WrapStylesheet(string body)
    {
        return
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <xsl:stylesheet version="1.0"
              xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
              xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
              xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
              xmlns:ext="urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"
              exclude-result-prefixes="cbc cac ext">

              <xsl:output method="html" encoding="UTF-8" indent="yes"/>

              <xsl:template match="/">
                <html>
                  <head>
                    <meta charset="UTF-8"/>
                    <style>
                      body { font-family: Arial, sans-serif; font-size: 11pt; margin: 20px; }
                      table { width: 100%; border-collapse: collapse; }
                      th, td { border: 1px solid #ccc; padding: 4px 8px; }
                      th { background: #e0e0e0; }
                      .highlight { font-weight: bold; }
                      .text-right { text-align: right; }
                      .divider-solid { border: none; border-top: solid; }
                      .divider-dashed { border: none; border-top: dashed; }
                      .divider-dotted { border: none; border-top: dotted; }
                    </style>
                  </head>
                  <body>
            """ + "\n" + body + "\n" +
            """
                  </body>
                </html>
              </xsl:template>

            </xsl:stylesheet>
            """;
    }

    // ── Body builder ──────────────────────────────────────────────────────

    private string BuildBody(BlockTreeDto tree)
    {
        var sb = new StringBuilder();
        foreach (var section in tree.Sections.OrderBy(s => s.Order))
        {
            sb.AppendLine($"    <!-- section: {XmlEscape(section.Name)} -->");
            foreach (var blockId in section.BlockIds)
            {
                if (!tree.Blocks.TryGetValue(blockId, out var block)) continue;
                sb.AppendLine(GenerateBlock(block, tree));
            }
        }
        return sb.ToString();
    }

    // ── Block dispatcher ─────────────────────────────────────────────────

    private string GenerateBlock(BlockDto block, BlockTreeDto tree)
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
            "Divider"      => GenerateDivider(block),
            "Spacer"       => GenerateSpacer(block),
            _              => $"<!-- unknown block type: {XmlEscape(block.Type)} -->"
        };
    }

    // ── BLOCK-01: Text ────────────────────────────────────────────────────

    private static string GenerateText(BlockDto block)
    {
        var cfg = Deserialize<TextConfig>(block.Config);
        if (cfg.IsStatic)
            return $"    <p>{XmlEscape(cfg.Content ?? string.Empty)}</p>";

        var xpath = cfg.Binding?.Xpath ?? string.Empty;
        var fallback = cfg.Binding?.Fallback;
        if (!string.IsNullOrEmpty(fallback))
            return $"""    <p><xsl:choose><xsl:when test="{XmlAttr(xpath)}"><xsl:value-of select="{XmlAttr(xpath)}"/></xsl:when><xsl:otherwise>{XmlEscape(fallback)}</xsl:otherwise></xsl:choose></p>""";

        return $"""    <p><xsl:value-of select="{XmlAttr(xpath)}"/></p>""";
    }

    // ── BLOCK-02: Heading ─────────────────────────────────────────────────

    private static string GenerateHeading(BlockDto block)
    {
        var cfg = Deserialize<HeadingConfig>(block.Config);
        var tag = cfg.Level.ToLowerInvariant(); // h1..h4

        if (cfg.IsStatic)
            return $"    <{tag}>{XmlEscape(cfg.Content ?? string.Empty)}</{tag}>";

        var xpath = cfg.Binding?.Xpath ?? string.Empty;
        return $"""    <{tag}><xsl:value-of select="{XmlAttr(xpath)}"/></{tag}>""";
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

    private static string GenerateTable(BlockDto block)
    {
        var cfg = Deserialize<TableConfig>(block.Config);
        var sb = new StringBuilder();
        var headerBg = cfg.HeaderBackgroundColor ?? "#E0E0E0";
        var altColor = cfg.AlternateRowColor ?? "#F9F9F9";

        sb.AppendLine("    <table>");

        if (cfg.ShowHeader)
        {
            sb.AppendLine($"      <thead><tr style=\"background:{XmlEscape(headerBg)}\">");
            foreach (var col in cfg.Columns)
            {
                var width = !string.IsNullOrEmpty(col.Width) ? $" style=\"width:{XmlEscape(col.Width)}\"" : string.Empty;
                sb.AppendLine($"        <th{width}>{XmlEscape(col.Header)}</th>");
            }
            sb.AppendLine("      </tr></thead>");
        }

        sb.AppendLine("      <tbody>");
        sb.AppendLine($"        <xsl:for-each select=\"{XmlAttr(cfg.IterateOver)}\">");
        sb.AppendLine($"          <xsl:variable name=\"pos\" select=\"position()\"/>");
        sb.AppendLine($"          <tr><xsl:if test=\"$pos mod 2 = 0\"><xsl:attribute name=\"style\">background:{XmlEscape(altColor)}</xsl:attribute></xsl:if>");
        foreach (var col in cfg.Columns)
            sb.AppendLine($"            <td><xsl:value-of select=\"{XmlAttr(col.Xpath)}\"/></td>");
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
            sb.AppendLine("      " + GenerateBlock(child, tree));
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
            // Simple xsl:if
            sb.AppendLine($"    <xsl:if test=\"{XmlAttr(test)}\">");
            foreach (var id in cfg.ThenBlockIds)
            {
                if (!tree.Blocks.TryGetValue(id, out var child)) continue;
                sb.AppendLine("      " + GenerateBlock(child, tree));
            }
            sb.Append("    </xsl:if>");
        }
        else
        {
            // xsl:choose
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
            "lessThan"    => $"number({xpath}) < {val}",   // XmlAttr &lt; olarak escape eder
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

        // Asset embed — Faz 5'te base64 ile doldurulacak; şimdilik placeholder
        return $"    <div style=\"{alignStyle}\"><img{widthAttr}{heightAttr} alt=\"{alt}\" src=\"\"/></div>";
    }

    // ── BLOCK-08: DocumentInfo ────────────────────────────────────────────

    private static string GenerateDocumentInfo(BlockDto block)
    {
        var cfg = Deserialize<DocumentInfoConfig>(block.Config);
        var sb = new StringBuilder();
        sb.AppendLine("    <table class=\"doc-info\">");
        sb.AppendLine("      <tbody>");
        foreach (var row in cfg.Rows)
        {
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td><strong>{XmlEscape(row.Label)}</strong></td>");
            sb.AppendLine($"          <td><xsl:value-of select=\"{XmlAttr(row.Xpath)}\"/></td>");
            sb.AppendLine("        </tr>");
        }
        sb.AppendLine("      </tbody>");
        sb.Append("    </table>");
        return sb.ToString();
    }

    // ── BLOCK-09: Totals ──────────────────────────────────────────────────

    private static string GenerateTotals(BlockDto block)
    {
        var cfg = Deserialize<TotalsConfig>(block.Config);
        var alignStyle = cfg.Alignment == "right" ? "text-align:right" : cfg.Alignment == "center" ? "text-align:center" : "text-align:left";
        var sb = new StringBuilder();
        sb.AppendLine($"    <table class=\"totals\" style=\"{alignStyle}\">");
        sb.AppendLine("      <tbody>");
        foreach (var row in cfg.Rows)
        {
            var cssClass = row.Highlight ? " class=\"highlight\"" : string.Empty;
            sb.AppendLine("        <tr>");
            sb.AppendLine($"          <td{cssClass}><strong>{XmlEscape(row.Label)}</strong></td>");
            sb.AppendLine($"          <td{cssClass}><xsl:value-of select=\"{XmlAttr(row.Xpath)}\"/></td>");
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

    private static string GenerateETTN(BlockDto block)
    {
        var cfg = Deserialize<EttnConfig>(block.Config);
        var sb = new StringBuilder("    <div class=\"ettn\">");
        sb.Append($"<p><strong>ETTN:</strong> <xsl:value-of select=\"{XmlAttr(cfg.EttnXpath)}\"/></p>");
        if (cfg.ShowQR)
            sb.Append("<div class=\"qr-placeholder\" style=\"width:80px;height:80px;border:1px solid #ccc;display:inline-block\">[QR]</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    // ── BLOCK-13: Divider ─────────────────────────────────────────────────

    private static string GenerateDivider(BlockDto block)
    {
        var cfg = Deserialize<DividerConfig>(block.Config);
        var style = new StringBuilder();
        style.Append($"border-top-style:{XmlEscape(cfg.Style)};");
        if (!string.IsNullOrEmpty(cfg.Color))       style.Append($"border-color:{XmlEscape(cfg.Color)};");
        if (!string.IsNullOrEmpty(cfg.Thickness))   style.Append($"border-top-width:{XmlEscape(cfg.Thickness)};");
        if (!string.IsNullOrEmpty(cfg.MarginTop))   style.Append($"margin-top:{XmlEscape(cfg.MarginTop)};");
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
