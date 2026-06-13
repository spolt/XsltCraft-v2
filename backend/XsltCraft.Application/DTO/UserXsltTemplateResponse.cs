namespace XsltCraft.Application.DTO;

public sealed class UserXsltTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Paylaşım bilgisi (liste rozeti için)
    public bool IsOwner { get; set; }
    public bool IsShared { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
}
