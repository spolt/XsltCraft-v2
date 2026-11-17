namespace XsltCraft.Application.DTO;

/// <summary>Seçili şablonların not bölümüne sabit not ekleme isteği.</summary>
public sealed class BulkAddFixedNoteRequest
{
    public List<Guid> Ids { get; set; } = [];

    public string NoteText { get; set; } = string.Empty;

    /// <summary>"replace" (varsayılan, idempotent) veya "append".</summary>
    public string Mode { get; set; } = "replace";

    /// <summary>"first": birden fazla Notlar bölümü bulunan şablonlarda kullanıcı onayıyla önce geleni kullan.</summary>
    public string? ResolveAmbiguous { get; set; }
}

public sealed class BulkAddFixedNoteResultResponse
{
    public List<BulkAddFixedNoteItemResult> Results { get; set; } = [];
}

public sealed class BulkAddFixedNoteItemResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>"updated" | "no_notes" | "ambiguous" | "locked" | "failed".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>"updated": notun eklendiği kopya sayısı (A5 çift kopyada 2).</summary>
    public int Copies { get; set; }
}
