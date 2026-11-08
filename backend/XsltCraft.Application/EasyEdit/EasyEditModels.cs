using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.EasyEdit;

/// <summary>Kullanıcının yüklediği XSLT'nin kolay düzenlemeye uygunluğu.</summary>
/// <remarks>Rejected: güvenlik denetiminden geçmedi. Unavailable: motor geçici olarak kullanılamadı (yeniden denenebilir).
/// Busy: kullanıcının eşzamanlı yürütme kotası dolu — yanıt gövdesine yazılmaz, uç nokta 429 döner.</remarks>
public enum EasyEditVerdict { Suitable, Partial, Unsuitable, Invalid, Rejected, Unavailable, Busy }

/// <summary>Görsel rolünün kaynağı: istemcinin seçimi, önceki analizin işareti ya da analizin önerisi.</summary>
public enum EasyEditRoleSource { Client, Marker, Suggested }

/// <param name="Index">Belge sırası (<see cref="IThemeSlotAnalyzer.DetectImages"/>).</param>
/// <param name="Movable">Yeri değiştirilebilir: gömülü raster data URL, nitelik değer şablonu yok
/// (<see cref="StaticImageSources"/>).</param>
/// <param name="StaticBlocker">Yeri değiştirilemiyorsa nedeni (<see cref="StaticImageSources"/> kodları); taşınabilirse null.</param>
/// <param name="SlotId">Görselin yeri konum olarak doğrulandıysa yuva kimliği.</param>
public sealed record EasyEditImage(int Index, ThemeImageRole Role, EasyEditRoleSource Source, string Reason, int Line,
    string? PreviewDataUrl, bool Movable, string? StaticBlocker, string? SlotId);

/// <summary>Metinde daha önce gömülmüş görsel bloğunun geri okunan içeriği.</summary>
/// <param name="FreeImages">Serbest görseller (<c>k:"free"</c>).</param>
public sealed record EasyEditPrevious(bool Recognized, string? Problem, IReadOnlyList<OverridesImageInfo> Images,
    IReadOnlyList<ThemeAssetKind> Hide, IReadOnlyList<BankInfoInput> Bank, IReadOnlyList<OverridesFreeImageInfo>? FreeImages = null)
{
    public IReadOnlyList<OverridesFreeImageInfo> FreeImages { get; init; } = FreeImages ?? [];
}

/// <param name="Xslt">İşaretli metin; yalnız Suitable ya da Partial kararında dolu.</param>
/// <param name="ErrorLine">Invalid kararında biliniyorsa hatanın satırı.</param>
/// <param name="DocumentBank">Belgenin kendi banka satırları (<see cref="DocumentBankDetector"/>); yalnız Suitable ya da
/// Partial kararında dolu.</param>
public sealed record EasyEditAnalysis(EasyEditVerdict Verdict, string? ReasonCode, string? Message, string? Xslt,
    DocumentType DocumentType, ThemeSlotCatalog? Catalog, IReadOnlyList<EasyEditImage> Images, EasyEditPrevious? Previous,
    IReadOnlyList<FixedNoteInput> Notes, bool NotesSupported, int? ErrorLine = null, DocumentBankInfo? DocumentBank = null);

/// <summary>Kullanıcının yüklediği XSLT'yi kolay düzenleme için analiz eder (yürütme <see cref="XsltRoute.Raw"/>).</summary>
public interface IUploadedXsltAnalyzer
{
    /// <param name="clientRoles">Kullanıcının seçtiği görsel rolleri (yanıttaki görsel sırasıyla); null → seçim yok.</param>
    Task<EasyEditAnalysis> AnalyzeAsync(string xslt, IReadOnlyList<ThemeImageRole>? clientRoles, Guid userId,
        CancellationToken cancellationToken);
}
