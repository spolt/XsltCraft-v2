namespace XsltCraft.Domain.Entities;

public class UserSnippet
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Prefix { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = "xslt";
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
