using XsltCraft.Application.ThemeSlots.Analysis;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Türü olmayan serbest görsel: yalnız seçilen yuvaya yerleşir, temadaki logo/imzayı gizlemez.</summary>
public sealed record FreeImageInput(string Slot, string DataUrl, int? Width, int? Height, ThemeAlignment Alignment);

internal sealed record FreeImageResult(IReadOnlyList<OverrideItem> Items, int AddedBytes, string? Error);

/// <summary>
/// Serbest görselleri görsel bloğu öğelerine çevirir (<c>{"k":"free","fb":[]}</c>). Geçersiz görsel kapalı
/// başarısızlıktır; yuva metinde yoksa, doluysa ya da koşulluysa uyarı verilir (uyarı türü yok). Temadaki görselin yeri
/// (<c>existing-*</c>) yalnız boş eklenmiş kutu olarak duruyorsa (görsel taşındı/silindi) kabul edilir.
/// </summary>
internal static class FreeImagePlacement
{
    public const string Kind = "free";
    public const string SlotNotFoundCode = "free-slot-not-found";
    private const string ExistingSlotPrefix = "existing-";
    private const string Label = "Serbest görsel";

    /// <summary>Biçimi geçerli yuva. Temadaki görselin yeri (<c>existing-*</c>) yerleşimde ayrıca denetlenir.</summary>
    public static bool IsAllowedSlot(string? slot) => slot is not null && ThemeSlotMarkers.SlotIdPattern.IsMatch(slot);

    /// <summary>Temadaki görselin yeri yalnız görsel taşınıp/silinip boş eklenmiş kutu olarak kaldıysa kullanılabilir.</summary>
    private static bool IsUsableThemeImageSlot(string slot, IReadOnlySet<string> emptyBoxSlots) =>
        !slot.StartsWith(ExistingSlotPrefix, StringComparison.Ordinal) || emptyBoxSlots.Contains(slot);

    /// <param name="slots">Eski blok hariç metindeki yuvalar.</param>
    /// <param name="emptyBoxSlots">Eski blok hariç metinde boş eklenmiş işaret kutusu olan yuvalar.</param>
    /// <param name="filledBoxSlots">Dosyadaki statik görselle dolan yuvalar.</param>
    /// <param name="assetSlots">Logo/imzanın yerleştiği yuvalar.</param>
    public static FreeImageResult Resolve(IReadOnlyList<FreeImageInput>? free, IReadOnlyCollection<string> slots,
        IReadOnlySet<string> emptyBoxSlots, IReadOnlyCollection<string> filledBoxSlots, IReadOnlyCollection<string> assetSlots, IReadOnlySet<string>? conditional,
        IThemeImageValidator images, List<ThemeComposeWarning> warnings)
    {
        var items = new List<OverrideItem>();
        var bytes = 0;
        foreach (var image in free ?? [])
        {
            var check = images.Check(image.DataUrl);
            if (!check.Valid)
                return new FreeImageResult([], 0, $"{Label}: {check.Error}");

            if (!IsAllowedSlot(image.Slot) || !slots.Contains(image.Slot))
            {
                warnings.Add(new ThemeComposeWarning(SlotNotFoundCode, $"{Label} için seçilen konum bu şablonda yok; görsel eklenmedi."));
                continue;
            }
            if (filledBoxSlots.Contains(image.Slot) || assetSlots.Contains(image.Slot))
            {
                warnings.Add(new ThemeComposeWarning(ThemeAssetSlotRules.SlotOccupiedCode,
                    $"{Label} için seçilen konum dolu; bu konum atlandı."));
                continue;
            }
            // Temadaki görsel hâlâ yerindeyse (yer tutucu img) serbest görsel onun yerini almaz.
            if (!IsUsableThemeImageSlot(image.Slot, emptyBoxSlots))
            {
                warnings.Add(new ThemeComposeWarning(SlotNotFoundCode, $"{Label} için seçilen konum bu şablonda yok; görsel eklenmedi."));
                continue;
            }
            if (conditional?.Contains(image.Slot) == true)
                warnings.Add(new ThemeComposeWarning("slot-conditional", $"{Label} için seçilen konum bazı faturalarda görünmeyebilir."));

            bytes += check.DecodedBytes;
            items.Add(new OverrideItem(Kind, image.Slot, [], image.DataUrl, image.Width, image.Height,
                image.Alignment.ToString().ToLowerInvariant()));
        }
        return new FreeImageResult(items, bytes, null);
    }
}
