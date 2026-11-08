using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit.Banks;

namespace XsltCraft.Application.ThemeSlots.Compose;

public static partial class ThemeComposeRequestValidator
{
    /// <summary>Belgedeki banka düzenlemesi listesinin üst sınırı (tespit edilebilen satır sayısı).</summary>
    public const int MaxDocumentBankEdits = DocumentBankDetector.MaxRows;

    private static ThemeComposeRequestError? ValidateDocumentBankEdits(ThemeComposeRequest r)
    {
        const string field = "documentBankEdits";
        if (r.DocumentBankEdits is not { } edits)
            return null;
        if (r.ThemeId is not null && edits.Count > 0)
            return new(field, "Belgedeki banka bilgisi yalnız XSLT içeriğiyle düzenlenebilir.");
        if (edits.Any(e => e?.BankName is null || e.Iban is null))
            return new(field, "Banka düzenleme listesi eksik ya da boş öğe içeriyor.");
        if (edits.Count > MaxDocumentBankEdits)
            return new(field, $"En fazla {MaxDocumentBankEdits} banka satırı düzenlenebilir.");
        if (edits.Count(e => e.Index is null) > MaxBankRows)
            return new(field, $"En fazla {MaxBankRows} banka hesabı eklenebilir.");

        var indexes = edits.Where(e => e.Index is not null).Select(e => e.Index!.Value).ToList();
        if (indexes.Distinct().Count() != indexes.Count)
            return new("documentBankEdits.index", "Her banka satırı bir kez düzenlenebilir.");
        foreach (var e in edits)
        {
            if (e.Index is < 0 or >= MaxDocumentBankEdits)
                return new("documentBankEdits.index", "Geçersiz banka satırı.");
            if (e.Remove && e.Index is null)
                return new("documentBankEdits.index", "Silinecek banka satırı belirtilmeli.");
            if (e.Remove)
                continue;
            if (e.BankName.Length > 100 || e.Iban.Length > 50)
                return new(field, "Banka adı en fazla 100, IBAN en fazla 50 karakter olabilir.");
            if ((e.BankName + e.Iban).Any(c => char.IsControl(c) || IsInvisibleFormatChar(c))
                || !IsXmlText(e.BankName) || !IsXmlText(e.Iban))
                return new(field, "Banka bilgisinde geçersiz karakter var.");
            if (!TrIban.Exact().IsMatch(e.Iban.Trim()))
                return new("documentBankEdits.iban", "IBAN 'TR' ile başlayan 26 karakterlik geçerli bir TR IBAN olmalı.");
        }

        // Belgedeki tabloya ekleme yapılırken önceki bloğun tablosu da yazılırsa banka bilgisi iki kez çıkardı.
        if (edits.Any(e => e.Index is null) && r.BankInfo is { Count: > 0 }
            && r.BankInfo.Any(b => !string.IsNullOrWhiteSpace(b?.BankName) || !string.IsNullOrWhiteSpace(b?.Iban)))
            return new(field, "Belgedeki banka tablosuna satır eklenirken ayrı banka tablosu (bankInfo) gönderilemez.");
        return null;
    }
}
