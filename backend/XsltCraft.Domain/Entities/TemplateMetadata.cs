namespace XsltCraft.Domain.Entities;

public class TemplateMetadata
{
    public List<string> Parameters { get; set; } = new();

    public List<string> Templates { get; set; } = new();

    public List<string> CallTemplates { get; set; } = new();

    public List<string> XPaths { get; set; } = new();
}