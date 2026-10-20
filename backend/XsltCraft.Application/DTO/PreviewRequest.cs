using XsltCraft.Application.Preview;

namespace XsltCraft.Application.DTO;

public class PreviewRequest
{
    /// <summary>Block tree formatı: null/1 = V1 (section-based), 2 = V2 (grid-based).</summary>
    public int? Version { get; set; }

    /// <summary>V2 için belge türü: "Invoice" (varsayılan) | "Despatch".</summary>
    public string? DocumentType { get; set; }

    // V1 fields
    public List<SectionDto> Sections { get; set; } = [];

    // Shared: both V1 and V2 use this
    public Dictionary<string, BlockDto> Blocks { get; set; } = [];

    public string XmlContent { get; set; } = "";
    // Assets: Faz 5'te doldurulacak — şimdilik kabul et, kullanma
    public object? Assets { get; set; }
}

public class ThemePreviewRequest
{
    public string XmlContent { get; set; } = "";
}

public class BankInfoItem
{
    public string BankName { get; set; } = "";
    public string Iban { get; set; } = "";
}

public class RawPreviewRequest
{
    public string Xslt { get; set; } = "";
    public string XmlContent { get; set; } = "";
}
