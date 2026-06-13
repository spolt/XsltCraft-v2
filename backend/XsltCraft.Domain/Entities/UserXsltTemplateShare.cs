namespace XsltCraft.Domain.Entities;

public class UserXsltTemplateShare
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public UserXsltTemplate Template { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid GrantedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
