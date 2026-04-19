namespace XsltCraft.Application.DTO;

public class UserSnippetResponse
{
    public Guid Id { get; set; }
    public string Prefix { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Description { get; set; }
    public string Scope { get; set; } = "xslt";
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateUserSnippetRequest
{
    public string Prefix { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Description { get; set; }
    public string Scope { get; set; } = "xslt";
    public bool IsPublic { get; set; }
}

public class UpdateUserSnippetRequest
{
    public string? Prefix { get; set; }
    public string? Body { get; set; }
    public string? Description { get; set; }
    public string? Scope { get; set; }
    public bool? IsPublic { get; set; }
}
