using System.Xml.Linq;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// Dosyanın kendi görselinin yerinin değiştirilebilmesi için tek kaynak: gömülü raster görsel (png, jpeg, jpg, gif,
/// webp, bmp; büyük/küçük harf duyarsız) ve nitelik değer şablonu (<c>{…}</c>) taşımayan <c>img</c>. SVG ve dış adres
/// kabul edilmez. Görsel içeriği doğrulanmaz (taşıma metni yeniden yazmaz, yalnız yerini değiştirir); kullanıcının
/// yüklediği görseller <see cref="IThemeImageValidator"/> ile ayrıca doğrulanır.
/// </summary>
internal static class StaticImageSources
{
    /// <summary>Görselin gömülü bir data URL kaynağı yok (dış adres, SVG ya da okunamayan veri).</summary>
    public const string NoDataUrl = "no-data-url";

    /// <summary>Gömülü görsel, raster beyaz listesi dışında bir biçimde.</summary>
    public const string FormatUnsupported = "format-unsupported";

    /// <summary>Görselin bir niteliği çalışma anında hesaplanıyor (nitelik değer şablonu).</summary>
    public const string DynamicAttribute = "dynamic-attribute";

    private const string DataImagePrefix = "data:image/";
    private const string Base64Marker = ";base64,";

    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase) { "png", "jpeg", "jpg", "gif", "webp", "bmp" };

    /// <summary>Kaynak beyaz listedeki bir raster data URL mi.</summary>
    public static bool IsAllowed(string? src) => FormatOf(src) is not null;

    /// <summary>Niteliklerden biri değer şablonu taşıyor (<c>{</c>; çözülmüş değer, varlıkla yazılmış ayraç da sayılır).</summary>
    public static bool HasDynamicAttribute(XElement img) => img.Attributes().Any(a => a.Value.Contains('{'));

    /// <summary>Yerinin değiştirilememe nedeni; taşınabilirse null.</summary>
    /// <param name="dataUrl">Analizin okuduğu gömülü kaynak; yoksa null.</param>
    public static string? Blocker(string? dataUrl, bool hasDynamicAttribute)
    {
        if (dataUrl is null)
            return NoDataUrl;
        if (!IsAllowed(dataUrl))
            return dataUrl.TrimStart().StartsWith(DataImagePrefix, StringComparison.OrdinalIgnoreCase) ? FormatUnsupported : NoDataUrl;
        return hasDynamicAttribute ? DynamicAttribute : null;
    }

    /// <summary>Data URL önekini küçük harfe çevirir, <c>jpg</c>'yi <c>jpeg</c> yapar; base64 verisi değişmez.
    /// Beyaz liste dışındaki değer olduğu gibi döner.</summary>
    public static string Normalize(string dataUrl)
    {
        if (FormatOf(dataUrl) is not { } format)
            return dataUrl;
        var trimmed = dataUrl.TrimStart();
        var data = trimmed[(trimmed.IndexOf(Base64Marker, StringComparison.OrdinalIgnoreCase) + Base64Marker.Length)..];
        var mime = format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpeg" : format.ToLowerInvariant();
        return $"{DataImagePrefix}{mime}{Base64Marker}{data}";
    }

    // data:image/<biçim>;base64, öneki; baştaki boşluk yok sayılır.
    private static string? FormatOf(string? src)
    {
        if (src is null)
            return null;
        var s = src.AsSpan().TrimStart();
        if (!s.StartsWith(DataImagePrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var rest = s[DataImagePrefix.Length..];
        var marker = rest.IndexOf(Base64Marker, StringComparison.OrdinalIgnoreCase);
        if (marker <= 0)
            return null;
        var format = rest[..marker].ToString();
        return Formats.Contains(format) ? format : null;
    }
}
