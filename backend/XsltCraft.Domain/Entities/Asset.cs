namespace XsltCraft.Domain.Entities;

public class Asset
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public AssetType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum AssetType
{
    Logo,
    Signature,
    Stamp,
    Custom
}
