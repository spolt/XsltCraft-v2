using System.Text;
using System.Xml;

using XsltCraft.Application.DTO;
using XsltCraft.Application.ThemeSlots.Analysis;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Biçim hatası: istemciye <c>{ error, field }</c> olarak döner (HTTP 400).</summary>
public sealed record ThemeComposeRequestError(string Field, string Message);

/// <summary>
/// <c>POST /api/preview/compose</c> isteğinin birleştirmeden önceki biçim doğrulaması (beyaz liste). Eksik liste,
/// boş öğe ve XML'e yazılamayan karakter sunucu hatası (500) yerine 400 olur.
/// </summary>
public static class ThemeComposeRequestValidator
{
    public const int MaxXsltBytes = 4 * 1024 * 1024;
    public const int MaxXmlBytes = 1024 * 1024;
    public const int MaxBankRows = 10;

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
            if (a.Width is < 10 or > 1200 || a.Height is < 10 or > 1200)
                return Error("assets.size", "Genişlik ve yükseklik 10–1200 piksel arasında olmalı.");
            if (a.Slot is not null && !ThemeSlotMarkers.SlotIdPattern.IsMatch(a.Slot))
                return Error("assets.slot", "Geçersiz konum kimliği.");
            if (a.DataUrl.Length > ThemeImageValidator.MaxDataUrlLength)
                return Error("assets.dataUrl", "Görsel çok büyük (en fazla 500 KB).");
        }

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

        return null;
    }

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
