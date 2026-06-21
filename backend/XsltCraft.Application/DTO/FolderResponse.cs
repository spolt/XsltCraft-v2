namespace XsltCraft.Application.DTO;

public sealed class FolderResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // "Draft" | "XsltTemplate"
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
}
