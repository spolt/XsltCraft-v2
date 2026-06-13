namespace XsltCraft.Application.DTO;

public sealed class UserSearchResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Email { get; set; } = string.Empty;
}
