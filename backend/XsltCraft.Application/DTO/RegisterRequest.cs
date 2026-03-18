namespace XsltCraft.Application.DTO;

public record RegisterRequest(string Email, string Password, string? DisplayName);
