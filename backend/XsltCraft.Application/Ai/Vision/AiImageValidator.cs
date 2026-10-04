using System.Buffers;

using XsltCraft.Application.Imaging;

namespace XsltCraft.Application.Ai.Vision;

/// <summary>İstemciden gelen ham görsel: beyan edilen MIME + base64 (data: öneki olmadan).</summary>
public record AiImagePayload(string? MimeType, string? Data);

public sealed record AiImageValidationResult(IReadOnlyList<AiImageInput> Images, string? ErrorCode, string? Message)
{
    public bool IsValid => ErrorCode is null;

    public static AiImageValidationResult Fail(string code, string message) => new([], code, message);
}

public interface IAiImageValidator
{
    /// <param name="maxCount">Kullanıcının etkin mesaj başına limiti (plan ∩ teknik tavan).</param>
    AiImageValidationResult Validate(IReadOnlyList<AiImagePayload> images, int maxCount);
}

/// <summary>
/// Sohbet görsellerini sağlayıcıya gitmeden önce doğrular: yalnız PNG/JPEG (magic byte = beyan MIME),
/// boyut başlıktan okunur (decode yok), bayt/piksel tavanları, base64 uzunluğu decode'dan ÖNCE kontrol edilir.
/// Başarıda kanonik base64 döner (sağlayıcılar yeniden kodlamaz).
/// </summary>
public sealed class AiImageValidator : IAiImageValidator
{
    public const string CodeCount = "image_count";
    public const string CodeTooLarge = "image_too_large";
    public const string CodeType = "image_type";
    public const string CodeMimeMismatch = "image_mime_mismatch";
    public const string CodeDimensions = "image_dimensions";
    public const string CodeInvalid = "image_invalid";

    private const string TypeMessage = "Yalnız PNG, JPG ve JPEG ekran görüntüleri kabul edilir.";

    private readonly VisionOptions _options;

    public AiImageValidator(VisionOptions options) => _options = options;

    public AiImageValidationResult Validate(IReadOnlyList<AiImagePayload> images, int maxCount)
    {
        if (images.Count == 0) return new([], null, null);
        if (images.Count > maxCount)
            return AiImageValidationResult.Fail(CodeCount, $"Mesaj başına en fazla {maxCount} ekran görüntüsü eklenebilir.");

        // Decode'dan önce ucuz boyut tahmini: hiçbir şey ayırmadan büyük gövdeyi reddet.
        // Tahmin padding nedeniyle gerçeği en fazla 2 bayt aşar; sınırda yanlış red olmasın.
        long estimatedTotal = 0;
        foreach (var img in images)
        {
            var estimated = EstimateDecodedLength(img.Data);
            if (estimated > _options.MaxImageBytes + 2) return TooLarge();
            estimatedTotal += estimated;
        }
        if (estimatedTotal > _options.MaxTotalBytes + 2L * images.Count) return TooLarge();

        var result = new List<AiImageInput>(images.Count);
        long total = 0;
        foreach (var img in images)
        {
            var single = ValidateOne(img);
            if (!single.IsValid) return single;

            var input = single.Images[0];
            total += input.ByteLength;
            if (total > _options.MaxTotalBytes) return TooLarge();
            result.Add(input);
        }

        return new(result, null, null);
    }

    private AiImageValidationResult ValidateOne(AiImagePayload img)
    {
        if (string.IsNullOrWhiteSpace(img.MimeType) || string.IsNullOrWhiteSpace(img.Data))
            return AiImageValidationResult.Fail(CodeInvalid, "Ekran görüntüsü boş ya da eksik.");
        if (img.Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return AiImageValidationResult.Fail(CodeInvalid, "Görsel verisi 'data:' öneki olmadan base64 gönderilmeli.");

        var declared = NormalizeMime(img.MimeType);
        if (declared is null) return AiImageValidationResult.Fail(CodeType, TypeMessage);

        var buffer = ArrayPool<byte>.Shared.Rent(img.Data.Length * 3 / 4 + 3);
        try
        {
            if (!Convert.TryFromBase64String(img.Data, buffer, out var written) || written == 0)
                return AiImageValidationResult.Fail(CodeInvalid, "Ekran görüntüsü verisi geçerli base64 değil.");
            if (written > _options.MaxImageBytes) return TooLarge();

            var bytes = buffer.AsSpan(0, written);
            var format = ImageHeaderReader.DetectFormat(bytes);
            if (format == ImageFormat.Unknown) return AiImageValidationResult.Fail(CodeType, TypeMessage);
            if (ImageHeaderReader.MimeTypeOf(format) != declared)
                return AiImageValidationResult.Fail(CodeMimeMismatch, "Görselin içeriği beyan edilen türle uyuşmuyor.");
            if (format == ImageFormat.Png && ImageHeaderReader.IsAnimatedPng(bytes))
                return AiImageValidationResult.Fail(CodeType, "Animasyonlu PNG kabul edilmez.");

            if (!ImageHeaderReader.TryReadDimensions(bytes, format, out var width, out var height))
                return AiImageValidationResult.Fail(CodeInvalid, "Görsel boyutu okunamadı; dosya bozuk olabilir.");
            if (Math.Max(width, height) > _options.MaxLongEdgePx || (long)width * height > _options.MaxPixels)
                return AiImageValidationResult.Fail(CodeDimensions,
                    $"Görsel çok büyük ({width}×{height}). Uzun kenar en fazla {_options.MaxLongEdgePx} piksel olmalı.");

            // Boşluk/satır sonu içeren base64'ü kanonik hale getir (sağlayıcılar katı).
            var canonicalLength = (written + 2) / 3 * 4;
            var base64 = img.Data.Length == canonicalLength ? img.Data : Convert.ToBase64String(bytes);

            return new([new AiImageInput(declared, base64, width, height, written)], null, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private AiImageValidationResult TooLarge()
        => AiImageValidationResult.Fail(CodeTooLarge,
            $"Ekran görüntüsü çok büyük. Görsel başına en fazla {_options.MaxImageBytes / 1024 / 1024.0:0.#} MB, toplam {_options.MaxTotalBytes / 1024 / 1024.0:0.#} MB.");

    private static long EstimateDecodedLength(string? base64)
        => string.IsNullOrEmpty(base64) ? 0 : (long)base64.Length * 3 / 4;

    private static string? NormalizeMime(string mime) => mime.Trim().ToLowerInvariant() switch
    {
        "image/png" => "image/png",
        "image/jpeg" or "image/jpg" => "image/jpeg",
        _ => null,
    };
}
