namespace XsltCraft.Application.DTO;

public sealed class TemplateShareResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Email { get; set; } = string.Empty;
}
