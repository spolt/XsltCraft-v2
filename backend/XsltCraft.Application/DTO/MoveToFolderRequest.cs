namespace XsltCraft.Application.DTO;

/// <summary>Şablonu bir klasöre taşır; <see cref="FolderId"/> null → klasörden çıkar ("Tümü").</summary>
public sealed class MoveToFolderRequest
{
    public Guid? FolderId { get; set; }
}
