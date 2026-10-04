using System.Buffers.Binary;

namespace XsltCraft.Application.Imaging;

public enum ImageFormat
{
    Unknown,
    Png,
    Jpeg,
}

/// <summary>
/// Görsel türünü magic byte'tan, boyutunu başlıktan okur (saf, kütüphanesiz). Piksel decode edilmez —
/// decompression bomb'a karşı boyut, gövde açılmadan kontrol edilebilir. Yalnız PNG ve JPEG desteklenir.
/// </summary>
public static class ImageHeaderReader
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Bozuk/kötü niyetli dosyada sonsuz tarama olmasın.
    private const int MaxSegments = 1024;

    public static ImageFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature)) return ImageFormat.Png;
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ImageFormat.Jpeg;
        return ImageFormat.Unknown;
    }

    public static string? MimeTypeOf(ImageFormat format) => format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        _ => null,
    };

    public static bool TryReadDimensions(ReadOnlySpan<byte> data, ImageFormat format, out int width, out int height)
    {
        width = height = 0;
        return format switch
        {
            ImageFormat.Png => TryReadPng(data, out width, out height),
            ImageFormat.Jpeg => TryReadJpeg(data, out width, out height),
            _ => false,
        };
    }

    /// <summary>APNG: ilk IDAT'tan önce acTL chunk'ı varsa animasyonludur.</summary>
    public static bool IsAnimatedPng(ReadOnlySpan<byte> data)
    {
        var offset = 8;
        for (var i = 0; i < MaxSegments && offset + 8 <= data.Length; i++)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            var type = data.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8)) return true;
            if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8)) return false;

            var next = (long)offset + 12 + length;
            if (next > data.Length) return false;
            offset = (int)next;
        }
        return false;
    }

    private static bool TryReadPng(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = height = 0;
        // İmza(8) + IHDR uzunluk(4) + "IHDR"(4) + genişlik(4) + yükseklik(4)
        if (data.Length < 24 || !data.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;

        var w = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        var h = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        if (w == 0 || h == 0 || w > int.MaxValue || h > int.MaxValue) return false;

        width = (int)w;
        height = (int)h;
        return true;
    }

    private static bool TryReadJpeg(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = height = 0;
        var pos = 2; // SOI (FF D8) sonrası

        for (var i = 0; i < MaxSegments; i++)
        {
            // Marker'a hizalan: 0xFF + (dolgu 0xFF'ler) + marker kodu.
            while (pos < data.Length && data[pos] != 0xFF) pos++;
            while (pos < data.Length && data[pos] == 0xFF) pos++;
            if (pos >= data.Length) return false;

            var marker = data[pos];
            pos++;

            // Uzunluk alanı olmayan bağımsız marker'lar: TEM, RSTn, SOI.
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD8) continue;
            // SOF'tan önce EOI/SOS → boyut yok.
            if (marker is 0xD9 or 0xDA) return false;

            if (pos + 2 > data.Length) return false;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, 2));
            if (segmentLength < 2) return false;

            // SOF0–SOF15; C4 (DHT), C8 (JPG), CC (DAC) SOF değildir.
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                // uzunluk(2) + hassasiyet(1) + yükseklik(2) + genişlik(2)
                if (pos + 7 > data.Length) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 5, 2));
                return width > 0 && height > 0;
            }

            pos += segmentLength;
        }
        return false;
    }
}
