using XsltCraft.Application.Preview;

namespace XsltCraft.Application.DTO;

public class PreviewRequest
{
    public List<SectionDto> Sections { get; set; } = [];
    public Dictionary<string, BlockDto> Blocks { get; set; } = [];
    public string XmlContent { get; set; } = "";
    // Assets: Faz 5'te doldurulacak — şimdilik kabul et, kullanma
    public object? Assets { get; set; }
}

public class ThemePreviewRequest
{
    public string XmlContent { get; set; } = "";
    public string? LogoUrl { get; set; }
    public int? LogoWidth { get; set; }
    public int? LogoHeight { get; set; }
    public string? LogoAlignment { get; set; }   // left | center | right
    public string? SignatureUrl { get; set; }
    public int? SignatureWidth { get; set; }
    public int? SignatureHeight { get; set; }
    public string? SignatureAlignment { get; set; } // left | center | right
    public List<BankInfoItem> BankInfo { get; set; } = [];
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
    public string? LogoUrl { get; set; }
    public int? LogoWidth { get; set; }
    public int? LogoHeight { get; set; }
    public string? LogoAlignment { get; set; }
    public string? SignatureUrl { get; set; }
    public int? SignatureWidth { get; set; }
    public int? SignatureHeight { get; set; }
    public string? SignatureAlignment { get; set; }
    public List<BankInfoItem> BankInfo { get; set; } = [];
}
