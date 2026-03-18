namespace XsltCraft.Application.DTO;

public record AuthResponse(string AccessToken, string TokenType = "Bearer");
