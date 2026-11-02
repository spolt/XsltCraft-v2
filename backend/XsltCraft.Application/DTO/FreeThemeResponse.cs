using XsltCraft.Application.ThemeSlots.Catalog;

namespace XsltCraft.Application.DTO;

public class FreeThemeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    /// <summary>true → ücretli tema (yalnız Pro/Editör/Admin "kullan"abilir); false → ücretsiz.</summary>
    public bool IsPremium { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Admin tema yanıtı: yükleme sonrası logo/imza konum analizi raporu (eski temalarda null).</summary>
public class AdminThemeResponse : FreeThemeResponse
{
    public ThemeSlotReportDto? SlotAnalysis { get; set; }
}
