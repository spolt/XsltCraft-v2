namespace XsltCraft.Domain.Entities;

public class UserActivity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserActivityType Type { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityKind { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

public enum UserActivityType
{
    Save,
    Download
}
