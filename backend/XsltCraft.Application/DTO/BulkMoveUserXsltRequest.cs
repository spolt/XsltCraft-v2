namespace XsltCraft.Application.DTO;

/// <summary>Seçili şablonları tek istekte bir klasöre taşır; <see cref="FolderId"/> null → klasörden çıkar.</summary>
public sealed class BulkMoveUserXsltRequest
{
    public List<Guid> Ids { get; set; } = [];

    public Guid? FolderId { get; set; }
}

public sealed class BulkMoveResultResponse
{
    /// <summary>Sahip olunan ve artık hedefte olan şablonlar (zaten orada olanlar dahil).</summary>
    public List<Guid> MovedIds { get; set; } = [];
}
