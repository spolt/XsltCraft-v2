namespace XsltCraft.Domain.Entities;

public class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DocumentType DocumentType { get; set; }
    public bool IsFreeTheme { get; set; } = false;
    /// <summary>Tema kütüphanesindeki ücretli/ücretsiz ayrımı. Yalnız IsFreeTheme=true temalar için anlamlı:
    /// false → ücretsiz tema (herkes kullanır), true → ücretli tema (yalnız Pro/Editör/Admin "kullan"abilir).</summary>
    public bool IsPremium { get; set; } = false;
    public string? BlockTree { get; set; }
    public string? XsltStoragePath { get; set; }
    public string? ThumbnailUrl { get; set; }
    /// <summary>Kullanıcının atadığı düz klasör (null → "Tümü"). Klasör silinince SetNull olur.</summary>
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public bool IsFavorite { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum DocumentType
{
    Invoice,
    Despatch
}
