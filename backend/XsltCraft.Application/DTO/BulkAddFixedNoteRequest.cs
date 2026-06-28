namespace XsltCraft.Application.DTO;

/// <summary>Seçili şablonların not bölümüne sabit not ekleme isteği.</summary>
public sealed class BulkAddFixedNoteRequest
{
    public List<Guid> Ids { get; set; } = [];

    public string NoteText { get; set; } = string.Empty;

    /// <summary>"replace" (varsayılan, idempotent) veya "append".</summary>
    public string Mode { get; set; } = "replace";
}

public sealed class BulkAddFixedNoteResultResponse
{
    public List<BulkAddFixedNoteItemResult> Results { get; set; } = [];
}

public sealed class BulkAddFixedNoteItemResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>"updated" | "no_notes" | "locked" | "failed".</summary>
    public string Status { get; set; } = string.Empty;
}
