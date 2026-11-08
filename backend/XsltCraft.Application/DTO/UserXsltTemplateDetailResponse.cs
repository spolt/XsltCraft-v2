namespace XsltCraft.Application.DTO;

public sealed class UserXsltTemplateDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string XsltContent { get; set; } = string.Empty;
    public string? XmlContent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsOwner { get; set; }

    /// <summary><c>"editor"</c> veya <c>"easyEdit"</c>.</summary>
    public string LastEditMode { get; set; } = XsltEditModeNames.Editor;
}
