namespace XsltCraft.Domain.Entities;

public class UserAiUsage
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int TokensUsed { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
