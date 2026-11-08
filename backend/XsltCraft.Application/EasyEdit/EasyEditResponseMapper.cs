using XsltCraft.Application.DTO;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;

namespace XsltCraft.Application.EasyEdit;

/// <summary>Analiz sonucunu API yanıtına çevirir (enum değerleri metin; etiketler projector'dan).</summary>
public static class EasyEditResponseMapper
{
    public static EasyEditAnalyzeResponse ToResponse(EasyEditAnalysis a, IThemeSlotProjector projector)
    {
        var slots = projector.ForUser(a.Catalog, a.DocumentType);
        return new EasyEditAnalyzeResponse(
            Verdict: a.Verdict.ToString().ToLowerInvariant(),
            ReasonCode: a.ReasonCode,
            Message: a.Message,
            DocumentType: a.DocumentType.ToString(),
            Slots: slots,
            ConditionalSlots: projector.ConditionalSlots(a.Catalog).Order(StringComparer.Ordinal).ToList(),
            Images: a.Images.Select(i => new EasyEditImageDto(i.Index, ThemeSlotProjector.RoleValue(i.Role),
                i.Source.ToString().ToLowerInvariant(), i.Reason, i.Line,
                i.PreviewDataUrl is null ? null : StaticImageSources.Normalize(i.PreviewDataUrl), i.Movable, i.StaticBlocker, i.SlotId)).ToList(),
            Previous: a.Previous is { } p ? Previous(p) : null,
            Features: slots.GroupBy(s => s.Group).Select(g => new EasyEditFeatureDto(g.Key, g.Count())).ToList(),
            Warnings: UserWarnings(a).Select(w => new ThemeSlotWarningDto(w.Code, w.Message, w.SlotId)).ToList(),
            Notes: a.Notes.Select(Note).ToList(),
            NotesSupported: a.NotesSupported,
            Xslt: a.Xslt,
            Error: a.Verdict == EasyEditVerdict.Invalid ? new EasyEditErrorDto(a.ErrorLine) : null,
            DocumentBank: a.DocumentBank is { } bank
                ? new EasyEditDocumentBankDto(bank.Found, bank.CanAdd,
                    bank.Rows.Select(r => new EasyEditDocumentBankRowDto(r.Index, r.BankName, r.Iban, r.BankNameEditable, r.Deletable, r.Line)).ToList())
                : null);
    }

    // Başarısızlık kodlu iç uyarılar kullanıcıya gitmez; Rejected ve Unavailable kararında hiç uyarı dönmez.
    private static IEnumerable<ThemeSlotWarning> UserWarnings(EasyEditAnalysis a) =>
        a.Verdict is EasyEditVerdict.Rejected or EasyEditVerdict.Unavailable or EasyEditVerdict.Busy
            ? []
            : (a.Catalog?.Warnings ?? []).Where(w => !ThemeSlotAnalyzer.IsFailureCode(w.Code));

    private static EasyEditPreviousDto Previous(EasyEditPrevious p) => new(
        p.Recognized,
        p.Problem,
        p.Images.Select(Image).ToList(),
        p.Hide.Select(h => ThemeSlotProjector.KindValue(h)).OfType<string>().ToList(),
        p.Bank.Select(b => new EasyEditBankRowDto(b.BankName, b.Iban)).ToList(),
        p.FreeImages.Select(f => new EasyEditPreviousFreeImageDto(f.Slot, f.DataUrl, f.Width, f.Height, f.Alignment, f.Valid, f.Problem)).ToList());

    private static EasyEditPreviousImageDto Image(OverridesImageInfo i) =>
        new(ThemeSlotProjector.KindValue(i.Kind), i.Slot, i.DataUrl, i.Width, i.Height, i.Alignment, i.Valid, i.Problem);

    private static ThemeComposeFixedNoteDto Note(FixedNoteInput n) => new()
    {
        Position = n.Position == FixedNotePosition.Before ? "before" : "after",
        Text = n.Text,
        Bold = n.Bold,
        Italic = n.Italic,
        Underline = n.Underline,
        FontSize = n.FontSize,
        Color = n.Color,
        FontFamily = n.FontFamily,
        Align = n.Align.ToString().ToLowerInvariant(),
    };
}
