namespace XsltCraft.Domain.Entities;

/// <summary>
/// Kullanıcının "Taslaklarım" (Draft) ve "Şablonlarım" (XsltTemplate) alanlarını
/// düzenlediği düz (tek seviye) klasör. <see cref="Kind"/> ile alana göre izoledir;
/// bir klasör yalnız kendi türündeki şablonlara atanabilir.
/// </summary>
public class Folder
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public FolderKind Kind { get; set; }
    /// <summary>Opsiyonel renk tonu (UI etiketi için, ör. "blue", "amber"). null → varsayılan.</summary>
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum FolderKind
{
    /// <summary>Grid-canvas blok şablonları (Template) — "Taslaklarım".</summary>
    Draft,
    /// <summary>Ham XSLT şablonları (UserXsltTemplate) — "Şablonlarım".</summary>
    XsltTemplate
}
