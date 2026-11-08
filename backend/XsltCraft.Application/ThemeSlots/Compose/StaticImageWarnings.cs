namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Statik görsel düzenleme uyarıları: kod ve tür başına tek uyarı, etkilenen görsellerin numarasıyla.</summary>
internal sealed class StaticImageWarnings
{
    public const string NotFound = "static-image-not-found";
    public const string SlotNotFound = "slot-not-found";
    public const string Ignored = "static-image-ignored";
    public const string Conflict = "static-image-conflict";
    public const string Skipped = "static-images-skipped";
    public const string NotEditable = "static-image-not-editable";
    public const string TooLarge = "static-image-too-large";

    private readonly List<(string Code, ThemeAssetKind? Kind, SortedSet<int> Numbers)> _items = [];

    public void Add(string code, int index, ThemeAssetKind? kind)
    {
        var item = _items.FirstOrDefault(i => i.Code == code && i.Kind == kind);
        if (item.Numbers is null)
            _items.Add(item = (code, kind, new SortedSet<int>()));
        item.Numbers.Add(index + 1);
    }

    public List<ThemeComposeWarning> ToList() =>
        _items.Select(i => new ThemeComposeWarning(i.Code, $"Görsel {string.Join(", ", i.Numbers)}: {Message(i.Code)}", i.Kind)).ToList();

    public static ThemeComposeWarning SkippedWarning() =>
        new(Skipped, "Dosyadaki görsellerde yapılan değişiklikler uygulanamadı; görseller yerinde bırakıldı.");

    private static string Message(string code) => code switch
    {
        NotFound => "bu şablonda bulunamadı; değişiklik uygulanmadı.",
        SlotNotFound => "seçilen konum bu şablonda yok; görsel yerinde bırakıldı.",
        Ignored => "aynı türde yeni görsel eklendiği için bu görseldeki değişiklik uygulanmadı.",
        NotEditable => "bu görselin yeri değiştirilemez; yalnız boyutu, hizası değiştirilebilir ya da silinebilir.",
        TooLarge => "bu konuma yerleştirmek şablonu izin verilen boyutun üstüne çıkarırdı; atlandı.",
        _ => "başka bir değişiklikle çakıştığı için yerinde bırakıldı.",
    };
}
