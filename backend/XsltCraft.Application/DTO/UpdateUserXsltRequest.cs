namespace XsltCraft.Application.DTO;

public sealed class UpdateUserXsltRequest
{
    public string? Name { get; set; }
    public string? XsltContent { get; set; }
    public string? XmlContent { get; set; }
}
