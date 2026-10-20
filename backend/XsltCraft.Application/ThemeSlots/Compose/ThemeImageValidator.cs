using System.Text.RegularExpressions;

using XsltCraft.Application.Imaging;

namespace XsltCraft.Application.ThemeSlots.Compose;

public sealed record ThemeImageCheck(bool Valid, string? Error, int DecodedBytes, string? MimeType);

/// <summary>
/// Temaya gömülecek görsel (data URL) doğrulaması: yalnız PNG/JPEG, base64 karakter kümesi, çözülmüş boyut
/// sınırı ve içerik (magic byte) türüyle eşleşme — <see cref="ImageUpload.Validate"/> ile aynı kural. SVG kabul edilmez.
/// </summary>
public interface IThemeImageValidator
{
    ThemeImageCheck Check(string? dataUrl);
}

public sealed partial class ThemeImageValidator : IThemeImageValidator
{
    public const int MaxDecodedBytes = 500 * 1024;
    public const int MaxDataUrlLength = 700_000;
    private const int MaxLongEdgePx = 4000;

    // \z: '$' sondaki '\n'i de kabul ederdi.
    [GeneratedRegex(@"^data:image/(png|jpeg|jpg);base64,([A-Za-z0-9+/]+={0,2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex DataUrl();

    public ThemeImageCheck Check(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl))
            return Fail("Görsel boş.");
        if (dataUrl.Length > MaxDataUrlLength)
            return Fail("Görsel çok büyük (en fazla 500 KB).");

        var m = DataUrl().Match(dataUrl);
        if (!m.Success)
            return Fail("Yalnızca PNG ve JPG görseller kabul edilir.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(m.Groups[2].Value);
        }
        catch (FormatException)
        {
            return Fail("Görsel verisi bozuk.");
        }

        if (bytes.Length > MaxDecodedBytes)
            return Fail("Görsel çok büyük (en fazla 500 KB).");

        var ext = m.Groups[1].Value == "png" ? ".png" : ".jpg";
        var image = ImageUpload.Validate(bytes, ext, MaxLongEdgePx);
        return image.IsValid
            ? new ThemeImageCheck(true, null, bytes.Length, image.MimeType)
            : Fail(image.Error!);
    }

    private static ThemeImageCheck Fail(string error) => new(false, error, 0, null);
}
