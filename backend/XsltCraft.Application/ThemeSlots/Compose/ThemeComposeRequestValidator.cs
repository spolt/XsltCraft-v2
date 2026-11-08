using System.Text;
using System.Xml;

using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.ThemeSlots.Analysis;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Biçim hatası: istemciye <c>{ error, field }</c> olarak döner (HTTP 400).</summary>
public sealed record ThemeComposeRequestError(string Field, string Message);

/// <summary>
/// <c>POST /api/preview/compose</c> isteğinin birleştirmeden önceki biçim doğrulaması (beyaz liste). Eksik liste,
/// boş öğe ve XML'e yazılamayan karakter sunucu hatası (500) yerine 400 olur.
/// </summary>
public static partial class ThemeComposeRequestValidator
{
    public const int MaxXsltBytes = 4 * 1024 * 1024;
    public const int MaxXmlBytes = 1024 * 1024;
    public const int MaxBankRows = 10;
    public const int MaxConditionalSlots = 32;
    public const int MaxStaticImages = UploadedXsltAnalyzer.MaxImages;

    public static ThemeComposeRequestError? Validate(ThemeComposeRequest r)
    {
        static ThemeComposeRequestError Error(string field, string message) => new(field, message);

        if ((r.ThemeId is null) == string.IsNullOrEmpty(r.Xslt))
            return Error("themeId", "Tema kimliği ya da XSLT içeriğinden yalnız biri verilmeli.");
        if (!r.IncludeXslt && !r.IncludeHtml)
            return Error("includeHtml", "En az bir çıktı (xslt ya da html) istenmeli.");
        if (r.IncludeHtml && string.IsNullOrWhiteSpace(r.XmlContent))
            return Error("xmlContent", "Önizleme için XML içeriği gerekli.");
        if (r.Xslt is not null && Encoding.UTF8.GetByteCount(r.Xslt) > MaxXsltBytes)
            return Error("xslt", "XSLT içeriği çok büyük (maks. 4 MB).");
        if (r.XmlContent is not null && Encoding.UTF8.GetByteCount(r.XmlContent) > MaxXmlBytes)
            return Error("xmlContent", "XML içeriği çok büyük (maks. 1 MB).");

        // JSON'daki null liste/öğe (ör. "assets": [null]) aşağıdaki erişimlerde NullReferenceException olurdu.
        if (r.Assets is null || r.Assets.Any(a => a?.DataUrl is null))
            return Error("assets", "Görsel listesi eksik ya da boş öğe içeriyor.");
        if (r.Assets.Count > 2 || r.Assets.Select(a => a.Kind).Distinct().Count() != r.Assets.Count)
            return Error("assets", "En fazla bir logo ve bir imza eklenebilir.");
        foreach (var a in r.Assets)
        {
            if (a.Kind is not ("logo" or "signature"))
                return Error("assets.kind", "Görsel türü 'logo' ya da 'signature' olmalı.");
            if (a.Alignment is not ("left" or "center" or "right"))
                return Error("assets.alignment", "Hizalama 'left', 'center' ya da 'right' olmalı.");
            if (!IsValidSize(a.Width, a.Height))
                return Error("assets.size", SizeMessage);
            if (a.Slot is not null && !ThemeSlotMarkers.SlotIdPattern.IsMatch(a.Slot))
                return Error("assets.slot", "Geçersiz konum kimliği.");
            if (a.DataUrl.Length > ThemeImageValidator.MaxDataUrlLength)
                return Error("assets.dataUrl", "Görsel çok büyük (en fazla 500 KB).");
        }

        if (ValidateStaticImages(r) is { } staticError)
            return staticError;
        if (ValidateFreeImages(r) is { } freeError)
            return freeError;

        if (r.ConditionalSlots is { } conditional
            && (conditional.Count > MaxConditionalSlots || conditional.Any(s => s is null || !ThemeSlotMarkers.SlotIdPattern.IsMatch(s))))
            return Error("conditionalSlots", $"Koşullu konum listesi geçersiz (en fazla {MaxConditionalSlots} geçerli kimlik).");

        if (r.HideThemeImages is null)
            return Error("hideThemeImages", "Kaldırılacak görsel listesi eksik.");
        if (r.HideThemeImages.Count > 2 || r.HideThemeImages.Any(k => k is not ("logo" or "signature")))
            return Error("hideThemeImages", "Kaldırılacak görsel türü 'logo' ya da 'signature' olmalı.");

        if (r.BankInfo is null || r.BankInfo.Any(b => b?.BankName is null || b.Iban is null))
            return Error("bankInfo", "Banka bilgisi listesi eksik ya da boş öğe içeriyor.");
        if (r.BankInfo.Count > MaxBankRows)
            return Error("bankInfo", $"En fazla {MaxBankRows} banka hesabı eklenebilir.");
        foreach (var b in r.BankInfo)
        {
            if (b.BankName.Length > 100 || b.Iban.Length > 50)
                return Error("bankInfo", "Banka adı en fazla 100, IBAN en fazla 50 karakter olabilir.");
            if ((b.BankName + b.Iban).Any(char.IsControl))
                return Error("bankInfo", "Banka bilgisinde geçersiz karakter var.");
            if (!IsXmlText(b.BankName) || !IsXmlText(b.Iban))
                return Error("bankInfo", "Banka bilgisinde geçersiz karakter var.");
        }

        if (ValidateDocumentBankEdits(r) is { } documentBankError)
            return documentBankError;

        return ValidateFixedNotes(r.FixedNotes);
    }

    private const string SizeMessage = "Genişlik ve yükseklik 10–1200 piksel arasında olmalı.";

    private static bool IsValidSize(int? width, int? height) =>
        width is null or (>= 10 and <= 1200) && height is null or (>= 10 and <= 1200);

    private static ThemeComposeRequestError? ValidateStaticImages(ThemeComposeRequest r)
    {
        const string field = "staticImages";
        if (r.StaticImages is not { } images)
            return null;
        if (r.ThemeId is not null && images.Count > 0)
            return new(field, "Dosyadaki görseller yalnız XSLT içeriğiyle düzenlenebilir.");
        if (images.Any(i => i is null))
            return new(field, "Görsel düzenleme listesi boş öğe içeriyor.");
        if (images.Count > MaxStaticImages)
            return new(field, $"En fazla {MaxStaticImages} görsel düzenlenebilir.");
        if (images.Select(i => i.Index).Distinct().Count() != images.Count)
            return new(field, "Her görsel bir kez düzenlenebilir.");
        foreach (var i in images)
        {
            if (i.Index is < 0 or >= MaxStaticImages)
                return new("staticImages.index", "Geçersiz görsel sırası.");
            if (i.Slot is not null && !ThemeSlotMarkers.SlotIdPattern.IsMatch(i.Slot))
                return new("staticImages.slot", "Geçersiz konum kimliği.");
            if (!IsValidSize(i.Width, i.Height))
                return new("staticImages.size", SizeMessage);
            if (i.Alignment is not (null or "left" or "center" or "right"))
                return new("staticImages.alignment", "Hizalama 'left', 'center' ya da 'right' olmalı.");
        }
        return null;
    }

    private static ThemeComposeRequestError? ValidateFixedNotes(List<ThemeComposeFixedNoteDto>? notes)
    {
        const string field = "fixedNotes";
        if (notes is null)
            return null;
        if (notes.Any(n => n?.Position is null || n.Text is null || n.Color is null || n.FontFamily is null || n.Align is null))
            return new(field, "Sabit not listesi eksik ya da boş öğe içeriyor.");
        if (notes.Count > FixedNoteStyles.MaxNotes || notes.Select(n => n.Position).Distinct().Count() != notes.Count)
            return new(field, "En fazla iki sabit not eklenebilir (notların üstü ve altı birer kez).");
        foreach (var n in notes)
        {
            if (!FixedNoteStyles.TryParsePosition(n.Position, out _))
                return new(field, "Not konumu 'before' ya da 'after' olmalı.");
            if (n.Text.Length > FixedNoteStyles.MaxTextLength)
                return new(field, $"Not en fazla {FixedNoteStyles.MaxTextLength} karakter olabilir.");
            if (n.Text.Replace("\r\n", "\n").Count(c => c is '\n' or '\r') >= FixedNoteStyles.MaxLines)
                return new(field, $"Not en fazla {FixedNoteStyles.MaxLines} satır olabilir.");
            if (n.Text.Any(c => (char.IsControl(c) && c is not ('\n' or '\r')) || IsInvisibleFormatChar(c)) || !IsXmlText(n.Text))
                return new(field, "Not metninde geçersiz karakter var.");
            if (!FixedNoteStyles.FontSizes.Contains(n.FontSize))
                return new(field, "Geçersiz yazı boyutu.");
            if (!FixedNoteStyles.ColorPattern.IsMatch(n.Color))
                return new(field, "Renk #RRGGBB biçiminde olmalı.");
            if (!FixedNoteStyles.FontFamilies.ContainsKey(n.FontFamily))
                return new(field, "Geçersiz yazı tipi.");
            if (!FixedNoteStyles.TryParseAlign(n.Align, out _))
                return new(field, "Hizalama 'left', 'center' ya da 'right' olmalı.");
        }
        return null;
    }

    // Görünmez biçim karakterleri (bidi override U+202E, sıfır genişlikli boşluk U+200B) ve
    // satır/paragraf ayırıcıları (U+2028/U+2029) faturada görsel yanıltmaya izin verirdi.
    private static bool IsInvisibleFormatChar(char c) =>
        char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.Format
            or System.Globalization.UnicodeCategory.LineSeparator
            or System.Globalization.UnicodeCategory.ParagraphSeparator;

    // XML 1.0 karakter kümesi: U+FFFE/U+FFFF ve eşsiz vekil (surrogate) yazılamaz — banka tablosunun
    // serileştirilmesi hata verirdi. Geçerli vekil çifti (ör. emoji) kabul edilir.
    private static bool IsXmlText(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (XmlConvert.IsXmlChar(s[i]))
                continue;
            if (i + 1 < s.Length && XmlConvert.IsXmlSurrogatePair(lowChar: s[i + 1], highChar: s[i]))
            {
                i++;
                continue;
            }
            return false;
        }
        return true;
    }
}
