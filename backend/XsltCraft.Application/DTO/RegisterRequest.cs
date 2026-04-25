namespace XsltCraft.Application.DTO;

public record RegisterRequest(string Username, string Email, string Password, string? DisplayName);
