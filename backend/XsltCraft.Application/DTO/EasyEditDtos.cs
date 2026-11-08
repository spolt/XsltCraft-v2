using XsltCraft.Application.ThemeSlots.Catalog;

namespace XsltCraft.Application.DTO;

/// <summary><c>POST /api/easy-edit/analyze</c> isteği.</summary>
public sealed class EasyEditAnalyzeRequest
{
    public string? Xslt { get; set; }
    /// <summary>Kullanıcının seçtiği görsel rolleri (logo | signature | design), yanıttaki görsel sırasıyla; yalnız seçim yapıldıysa.</summary>
    public List<string>? ImageRoles { get; set; }
}

/// <param name="Role">logo | signature | design.</param>
/// <param name="Source">client | marker | suggested.</param>
/// <param name="Blocker">Yeri değiştirilemiyorsa nedeni: no-data-url | format-unsupported | dynamic-attribute; taşınabilirse null.</param>
public sealed record EasyEditImageDto(int Index, string Role, string Source, string Reason, int Line, string? PreviewDataUrl,
    bool Movable, string? Blocker, string? SlotId);

/// <param name="Kind">logo | signature; tanınmayan görselde null.</param>
public sealed record EasyEditPreviousImageDto(string? Kind, string? Slot, string? DataUrl, int? Width, int? Height, string Alignment, bool Valid, string? Problem);

public sealed record EasyEditBankRowDto(string BankName, string Iban);

/// <summary>Önceki bloktaki serbest görsel; <c>Valid=false</c> ise <c>DataUrl</c> null, <c>Problem</c> dolu.</summary>
/// <param name="Alignment">left | center | right.</param>
public sealed record EasyEditPreviousFreeImageDto(string Slot, string? DataUrl, int? Width, int? Height, string Alignment, bool Valid, string? Problem);

/// <param name="Hide">logo | signature.</param>
public sealed record EasyEditPreviousDto(bool Recognized, string? Problem, IReadOnlyList<EasyEditPreviousImageDto> Images,
    IReadOnlyList<string> Hide, IReadOnlyList<EasyEditBankRowDto> Bank, IReadOnlyList<EasyEditPreviousFreeImageDto> FreeImages);

/// <summary>Belgenin kendi banka satırı.</summary>
/// <param name="Index">Compose isteğindeki <c>documentBankEdits[].index</c>.</param>
/// <param name="BankNameEditable">false → ad değiştirilemez (yalnız IBAN).</param>
/// <param name="Deletable">false → satır silinemez.</param>
public sealed record EasyEditDocumentBankRowDto(int Index, string BankName, string Iban, bool BankNameEditable, bool Deletable, int Line);

/// <param name="Found">Belgede düzenlenebilir banka satırı bulundu.</param>
/// <param name="CanAdd">Belgedeki tabloya yeni satır eklenebilir.</param>
public sealed record EasyEditDocumentBankDto(bool Found, bool CanAdd, IReadOnlyList<EasyEditDocumentBankRowDto> Rows);

/// <summary>Konum grubu ve o gruptaki konum sayısı (çipler için).</summary>
public sealed record EasyEditFeatureDto(string Group, int Count);

public sealed record EasyEditErrorDto(int? Line);

/// <param name="Verdict">suitable | partial | unsuitable | invalid | rejected | unavailable.</param>
/// <param name="DocumentType">Invoice | Despatch.</param>
/// <param name="Error">Yalnız invalid kararında.</param>
/// <param name="DocumentBank">Belgenin kendi banka satırları; yalnız suitable/partial kararında.</param>
public sealed record EasyEditAnalyzeResponse(
    string Verdict,
    string? ReasonCode,
    string? Message,
    string DocumentType,
    IReadOnlyList<ThemeSlotOptionDto> Slots,
    IReadOnlyList<string> ConditionalSlots,
    IReadOnlyList<EasyEditImageDto> Images,
    EasyEditPreviousDto? Previous,
    IReadOnlyList<EasyEditFeatureDto> Features,
    IReadOnlyList<ThemeSlotWarningDto> Warnings,
    IReadOnlyList<ThemeComposeFixedNoteDto> Notes,
    bool NotesSupported,
    string? Xslt,
    EasyEditErrorDto? Error,
    EasyEditDocumentBankDto? DocumentBank = null);
