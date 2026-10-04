namespace XsltCraft.Application.Imaging;

public sealed record ImageUploadResult(string? Error, ImageFormat Format, string MimeType, string Extension, int Width, int Height)
{
    public bool IsValid => Error is null;

    internal static ImageUploadResult Fail(string error) => new(error, ImageFormat.Unknown, "", "", 0, 0);
}

/// <summary>
/// Sunucuda saklanıp servis edilen görsel yüklemeleri (asset, tema thumbnail'i) için tek doğrulama:
/// yalnız PNG/JPG/JPEG, içerik (magic byte) uzantıyla eşleşmeli, boyut başlıktan okunur.
/// MIME ve uzantı istemcinin beyanından DEĞİL içerikten türetilir — <c>text/html</c> beyanlı ya da
/// SVG içerikli bir ".png" aynı origin'den HTML/script olarak servis edilemez.
/// </summary>
public static class ImageUpload
{
    public static readonly IReadOnlyList<string> AllowedExtensions = [".png", ".jpg", ".jpeg"];

    public const string TypeMessage = "Yalnızca PNG, JPG ve JPEG dosyaları kabul edilir.";

    /// <param name="maxLongEdgePx">Uzun kenar üst sınırı (önizleme/indirilen XSLT'ye base64 gömülür).</param>
    public static ImageUploadResult Validate(ReadOnlySpan<byte> data, string? fileName, int maxLongEdgePx = 8_000)
    {
        if (data.IsEmpty) return ImageUploadResult.Fail("Dosya boş olamaz.");

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return ImageUploadResult.Fail(TypeMessage);

        var format = ImageHeaderReader.DetectFormat(data);
        var expected = ext == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
        if (format == ImageFormat.Unknown) return ImageUploadResult.Fail(TypeMessage);
        if (format != expected) return ImageUploadResult.Fail("Dosya içeriği uzantısıyla uyuşmuyor.");

        if (!ImageHeaderReader.TryReadDimensions(data, format, out var width, out var height))
            return ImageUploadResult.Fail("Görsel okunamadı; dosya bozuk olabilir.");
        if (Math.Max(width, height) > maxLongEdgePx)
            return ImageUploadResult.Fail($"Görsel çok büyük ({width}×{height}). Uzun kenar en fazla {maxLongEdgePx} piksel olmalı.");

        return new(null, format, ImageHeaderReader.MimeTypeOf(format)!, format == ImageFormat.Png ? ".png" : ".jpg", width, height);
    }

    /// <summary>
    /// Saklanan dosyanın servis MIME'ı — DB'deki (eski kayıtlarda istemci beyanı olan) MimeType yerine
    /// sunucunun yazdığı dosya uzantısından. Eski SVG asset'ler <c>image/svg+xml</c> döner; çağıran
    /// bunu sandbox CSP ile servis etmeli. Bilinmeyen → <c>application/octet-stream</c>.
    /// </summary>
    public static string ServeMimeType(string storagePath) => Path.GetExtension(storagePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream",
    };
}
