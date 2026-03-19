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

public sealed class BlockDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public JsonElement Config { get; set; }
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
}

public sealed class TableColumn
{
    [JsonPropertyName("header")]
    public string Header { get; set; } = string.Empty;

    [JsonPropertyName("xpath")]
    public string Xpath { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public string? Width { get; set; }
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
}

public sealed class NotesConfig
{
    [JsonPropertyName("iterateOver")]
    public string IterateOver { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }
}

public sealed class BankInfoConfig
{
    [JsonPropertyName("bankNameXpath")]
    public string? BankNameXpath { get; set; }

    [JsonPropertyName("ibanXpath")]
    public string? IbanXpath { get; set; }

    [JsonPropertyName("paymentTermsXpath")]
    public string? PaymentTermsXpath { get; set; }
}

public sealed class EttnConfig
{
    [JsonPropertyName("ettnXpath")]
    public string EttnXpath { get; set; } = string.Empty;

    [JsonPropertyName("showQR")]
    public bool ShowQR { get; set; }
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
