namespace XsltCraft.Application.DTO;

public sealed class CreateUserXsltRequest
{
    public string Name { get; set; } = "Yeni Şablon";
    public string XsltContent { get; set; } = string.Empty;
    public string? XmlContent { get; set; }
}
