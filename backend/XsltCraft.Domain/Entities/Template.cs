namespace XsltCraft.Domain.Entities;

public class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DocumentType DocumentType { get; set; }
    public bool IsFreeTheme { get; set; } = false;
    public string? BlockTree { get; set; }
    public string? XsltStoragePath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum DocumentType
{
    Invoice,
    Despatch
}
