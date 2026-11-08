using XsltCraft.Application.DTO;
using XsltCraft.Application.ThemeSlots.Overrides;

namespace XsltCraft.Application.ThemeSlots.Compose;

public static partial class ThemeComposeRequestValidator
{
    public const int MaxFreeImages = OverridesBlockParser.MaxFreeImages;

    private static ThemeComposeRequestError? ValidateFreeImages(ThemeComposeRequest r)
    {
        const string field = "freeImages";
        if (r.FreeImages is not { } free)
            return null;
        if (r.ThemeId is not null && free.Count > 0)
            return new(field, "Serbest görsel yalnız XSLT içeriğiyle eklenebilir.");
        if (free.Any(f => f?.DataUrl is null || f.Alignment is null))
            return new(field, "Serbest görsel listesi eksik ya da boş öğe içeriyor.");
        if (free.Count > MaxFreeImages)
            return new(field, $"En fazla {MaxFreeImages} serbest görsel eklenebilir.");
        foreach (var f in free)
        {
            if (!FreeImagePlacement.IsAllowedSlot(f.Slot))
                return new("freeImages.slot", "Serbest görsel için geçerli bir konum seçilmeli.");
            if (!IsValidSize(f.Width, f.Height))
                return new("freeImages.size", SizeMessage);
            if (f.Alignment is not ("left" or "center" or "right"))
                return new("freeImages.alignment", "Hizalama 'left', 'center' ya da 'right' olmalı.");
            if (f.DataUrl.Length > ThemeImageValidator.MaxDataUrlLength)
                return new("freeImages.dataUrl", "Görsel çok büyük (en fazla 500 KB).");
        }
        return null;
    }
}
