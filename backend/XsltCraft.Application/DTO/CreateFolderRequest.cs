namespace XsltCraft.Application.DTO;

public sealed class CreateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Draft"; // "Draft" | "XsltTemplate"
    public string? Color { get; set; }
}
