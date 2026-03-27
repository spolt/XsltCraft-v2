namespace XsltCraft.Domain.Entities;

public class UserXsltTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string XsltContent { get; set; } = string.Empty;
    public string? XmlContent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
