namespace XsltCraft.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public string? GoogleId { get; set; }
    public bool EmailVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public enum UserRole
{
    User,
    Admin
}
