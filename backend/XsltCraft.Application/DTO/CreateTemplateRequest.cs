namespace XsltCraft.Application.DTO;

public sealed class CreateTemplateRequest
{
    public string Name { get; set; } = "Yeni Şablon";
    public string DocumentType { get; set; } = "Invoice"; // "Invoice" | "Despatch"
    public string? BlockTree { get; set; }
}
