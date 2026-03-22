using System.Text.Json;
using System.Text.Json.Serialization;

namespace XsltCraft.Application.Preview;

// ── Top-level tree ──────────────────────────────────────────────────────────

public sealed class BlockTreeDto
{
    [JsonPropertyName("sections")]
    public List<SectionDto> Sections { get; set; } = [];

    [JsonPropertyName("blocks")]
    public Dictionary<string, BlockDto> Blocks { get; set; } = [];
}

public sealed class SectionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("layout")]
    public string Layout { get; set; } = "single-column";

    [JsonPropertyName("blockIds")]
    public List<string> BlockIds { get; set; } = [];
}

public sealed class BlockLayoutDto
{
    [JsonPropertyName("width")]
    public string Width { get; set; } = "full"; // full | 1/2 | 1/3 | 2/3

    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "left"; // left | center | right
}

public sealed class BlockDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public JsonElement Config { get; set; }

    [JsonPropertyName("layout")]
    public BlockLayoutDto? Layout { get; set; }
}

// ── Per-block config helpers ────────────────────────────────────────────────

public sealed class BindingConfig
{
    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("fallback")]
    public string? Fallback { get; set; }
}

public sealed class TextConfig
{
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("binding")]
    public BindingConfig? Binding { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    [JsonPropertyName("fontStyle")]
    public string? FontStyle { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public sealed class HeadingConfig
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "H2";

    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("binding")]
    public BindingConfig? Binding { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    [JsonPropertyName("fontStyle")]
    public string? FontStyle { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public sealed class ParagraphLine
{
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("xpath")]
    public string? Xpath { get; set; }
}

public sealed class ParagraphConfig
{
    [JsonPropertyName("lines")]
    public List<ParagraphLine> Lines { get; set; } = [];

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class TableColumn
{
    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public string? Width { get; set; }

    /// <summary>text | currency | number | date</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

public sealed class TableConfig
{
    [JsonPropertyName("iterateOver")]
    public string IterateOver { get; set; } = string.Empty;

    [JsonPropertyName("columns")]
    public List<TableColumn> Columns { get; set; } = [];

    [JsonPropertyName("showHeader")]
    public bool ShowHeader { get; set; } = true;

    [JsonPropertyName("alternateRowColor")]
    public string? AlternateRowColor { get; set; }

    [JsonPropertyName("headerBackgroundColor")]
    public string? HeaderBackgroundColor { get; set; }
}

public sealed class ForEachConfig
{
    [JsonPropertyName("iterateOver")]
    public string IterateOver { get; set; } = string.Empty;

    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = [];
}

public sealed class ConditionalCondition
{
    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public sealed class ConditionalConfig
{
    [JsonPropertyName("condition")]
    public ConditionalCondition Condition { get; set; } = new();

    [JsonPropertyName("thenBlockIds")]
    public List<string> ThenBlockIds { get; set; } = [];

    [JsonPropertyName("elseBlockIds")]
    public List<string> ElseBlockIds { get; set; } = [];
}

public sealed class ImageConfig
{
    [JsonPropertyName("assetId")]
    public string? AssetId { get; set; }

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "logo";

    [JsonPropertyName("altText")]
    public string? AltText { get; set; }

    [JsonPropertyName("width")]
    public string? Width { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "center";

    [JsonPropertyName("editableOnFreeTheme")]
    public bool EditableOnFreeTheme { get; set; }
}

public sealed class DocumentInfoRow
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;
}

public sealed class DocumentInfoConfig
{
    [JsonPropertyName("rows")]
    public List<DocumentInfoRow> Rows { get; set; } = [];

    [JsonPropertyName("bordered")]
    public bool? Bordered { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class TotalsRow
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("highlight")]
    public bool Highlight { get; set; }
}

public sealed class TotalsConfig
{
    [JsonPropertyName("rows")]
    public List<TotalsRow> Rows { get; set; } = [];

    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "right";

    [JsonPropertyName("labelWidth")]
    public string? LabelWidth { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class NotesConfig
{
    [JsonPropertyName("iterateOver")]
    public string IterateOver { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("bordered")]
    public bool? Bordered { get; set; }

    [JsonPropertyName("borderColor")]
    public string? BorderColor { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class BankInfoConfig
{
    [JsonPropertyName("bankName")]
    public string BankName { get; set; } = string.Empty;

    [JsonPropertyName("iban")]
    public string Iban { get; set; } = string.Empty;

    [JsonPropertyName("ibanLabel")]
    public string? IbanLabel { get; set; }

    [JsonPropertyName("bordered")]
    public bool? Bordered { get; set; }

    [JsonPropertyName("borderColor")]
    public string? BorderColor { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class EttnConfig
{
    [JsonPropertyName("ettnXpath")]
    public string EttnXpath { get; set; } = string.Empty;

    [JsonPropertyName("showQR")]
    public bool ShowQR { get; set; }

    /// <summary>QR kod genişliği (piksel). Varsayılan: 80.</summary>
    [JsonPropertyName("qrWidth")]
    public int QrWidth { get; set; } = 80;

    /// <summary>QR kod yüksekliği (piksel). Varsayılan: 80.</summary>
    [JsonPropertyName("qrHeight")]
    public int QrHeight { get; set; } = 80;

    /// <summary>QR kodun yatay hizalaması: left | center | right. Varsayılan: right.</summary>
    [JsonPropertyName("qrAlignment")]
    public string QrAlignment { get; set; } = "right";

    /// <summary>ETTN metnini göster/gizle. Varsayılan: true.</summary>
    [JsonPropertyName("showEttn")]
    public bool ShowEttn { get; set; } = true;
}

public sealed class DividerConfig
{
    [JsonPropertyName("style")]
    public string Style { get; set; } = "solid";

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("thickness")]
    public string? Thickness { get; set; }

    [JsonPropertyName("marginTop")]
    public string? MarginTop { get; set; }

    [JsonPropertyName("marginBottom")]
    public string? MarginBottom { get; set; }
}

public sealed class SpacerConfig
{
    [JsonPropertyName("height")]
    public string Height { get; set; } = "24px";
}

public sealed class VariableConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;
}

public sealed class ConditionalTextConfig
{
    [JsonPropertyName("condition")]
    public ConditionalCondition Condition { get; set; } = new();

    [JsonPropertyName("thenIsStatic")]
    public bool ThenIsStatic { get; set; }

    [JsonPropertyName("thenContent")]
    public string ThenContent { get; set; } = string.Empty;

    [JsonPropertyName("elseIsStatic")]
    public bool ElseIsStatic { get; set; }

    [JsonPropertyName("elseContent")]
    public string ElseContent { get; set; } = string.Empty;

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class GibLogoConfig
{
    [JsonPropertyName("width")]
    public string? Width { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("alignment")]
    public string Alignment { get; set; } = "center";
}

public sealed class InvoiceTotalsField
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("highlight")]
    public bool Highlight { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("isCustom")]
    public bool? IsCustom { get; set; }
}

public sealed class InvoiceTotalsConfig
{
    [JsonPropertyName("fields")]
    public List<InvoiceTotalsField> Fields { get; set; } = [];

    [JsonPropertyName("showCurrency")]
    public bool ShowCurrency { get; set; } = true;

    [JsonPropertyName("currencyXpath")]
    public string CurrencyXpath { get; set; } = "//cbc:DocumentCurrencyCode";

    [JsonPropertyName("labelWidth")]
    public string? LabelWidth { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class GibKarekodConfig
{
    [JsonPropertyName("qrWidth")]
    public int QrWidth { get; set; } = 150;

    [JsonPropertyName("qrHeight")]
    public int QrHeight { get; set; } = 150;

    /// <summary>left | center | right. Varsayılan: right.</summary>
    [JsonPropertyName("qrAlignment")]
    public string QrAlignment { get; set; } = "right";
}

public sealed class PartyInfoField
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("relativeXpath")]
    public string RelativeXpath { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("isCustom")]
    public bool? IsCustom { get; set; }
}

public sealed class PartyInfoConfig
{
    [JsonPropertyName("partyType")]
    public string PartyType { get; set; } = "SupplierParty";

    [JsonPropertyName("fields")]
    public List<PartyInfoField> Fields { get; set; } = [];

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("showTitle")]
    public bool ShowTitle { get; set; } = true;

    [JsonPropertyName("bordered")]
    public bool Bordered { get; set; } = true;

    [JsonPropertyName("labelStyle")]
    public string LabelStyle { get; set; } = "table";

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class InvoiceLineColumn
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("relativeXpath")]
    public string RelativeXpath { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public string? Width { get; set; }

    /// <summary>text | currency | number | percent</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("isCustom")]
    public bool? IsCustom { get; set; }
}

public sealed class InvoiceLineTableConfig
{
    [JsonPropertyName("iterateOver")]
    public string IterateOver { get; set; } = "//cac:InvoiceLine";

    [JsonPropertyName("columns")]
    public List<InvoiceLineColumn> Columns { get; set; } = [];

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("showTitle")]
    public bool ShowTitle { get; set; }

    [JsonPropertyName("showHeader")]
    public bool ShowHeader { get; set; } = true;

    [JsonPropertyName("showRowNumber")]
    public bool ShowRowNumber { get; set; } = true;

    [JsonPropertyName("bordered")]
    public bool Bordered { get; set; } = true;

    [JsonPropertyName("alternateRowColor")]
    public string? AlternateRowColor { get; set; }

    [JsonPropertyName("headerBackgroundColor")]
    public string? HeaderBackgroundColor { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class InvoiceHeaderField
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("isCustom")]
    public bool? IsCustom { get; set; }
}

public sealed class InvoiceHeaderConfig
{
    [JsonPropertyName("fields")]
    public List<InvoiceHeaderField> Fields { get; set; } = [];

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("showTitle")]
    public bool ShowTitle { get; set; }

    [JsonPropertyName("bordered")]
    public bool Bordered { get; set; } = true;

    [JsonPropertyName("labelStyle")]
    public string LabelStyle { get; set; } = "table";

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }
}

public sealed class TaxSummaryConfig
{
    [JsonPropertyName("taxTotalXpath")]
    public string TaxTotalXpath { get; set; } = "//cac:TaxTotal/cac:TaxSubtotal";

    [JsonPropertyName("percentXpath")]
    public string PercentXpath { get; set; } = "cac:TaxCategory/cbc:Percent";

    [JsonPropertyName("taxableAmountXpath")]
    public string TaxableAmountXpath { get; set; } = "cbc:TaxableAmount";

    [JsonPropertyName("taxAmountXpath")]
    public string TaxAmountXpath { get; set; } = "cbc:TaxAmount";

    [JsonPropertyName("showPercent")]
    public bool ShowPercent { get; set; } = true;

    [JsonPropertyName("headerBackgroundColor")]
    public string? HeaderBackgroundColor { get; set; }
}
