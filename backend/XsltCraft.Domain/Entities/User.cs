namespace XsltCraft.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public MembershipPlan Plan { get; set; } = MembershipPlan.Free;
    /// <summary>Pro abonelik bitiş anı (UTC). null = süresiz/Free. Geçmişse etkin plan Free'ye düşer.</summary>
    public DateTime? PlanExpiresAt { get; set; }
    public string? GoogleId { get; set; }
    public bool EmailVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UserActivity> Activities { get; set; } = [];
}

public enum UserRole
{
    User,
    Editor,
    Admin
}

public enum MembershipPlan
{
    Free,
    Pro
}
