using System.Buffers.Binary;
using System.Text;

namespace XsltCraft.Application.Tests.Imaging;

/// <summary>
/// Testler için elle kurulan minimal PNG/JPEG baytları. Yalnız başlık okunduğu için piksel verisi/CRC
/// geçerli olmak zorunda değil — IHDR'de 50000×50000 beyan eden "bomb" de birkaç bayttır.
/// </summary>
internal static class ImageFixtures
{
    public static byte[] Png(int width, int height, bool animated = false)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // truecolor
        WriteChunk(ms, "IHDR", ihdr);

        if (animated)
            WriteChunk(ms, "acTL", new byte[8]);

        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00]);
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    public static byte[] Jpeg(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xD8]);                         // SOI
        // APP0/JFIF (uzunluk 16)
        ms.Write([0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);
        // Dolgu bayt'ı + DHT (SOF değildir; atlanmalı) — tarayıcının sağlamlığını sınar.
        ms.Write([0xFF, 0xFF, 0xC4, 0x00, 0x04, 0x00, 0x00]);
        // SOF0: uzunluk 17, hassasiyet 8, yükseklik, genişlik, 3 bileşen
        ms.Write([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        ms.Write([(byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width]);
        ms.Write([0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);
        ms.Write([0xFF, 0xD9]);                         // EOI
        return ms.ToArray();
    }

    public static byte[] Gif() => "GIF89a\x01\x00\x01\x00"u8.ToArray();
    public static byte[] Svg() => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
    public static byte[] WebP() => "RIFF\x1a\x00\x00\x00WEBPVP8 "u8.ToArray();

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        s.Write(Encoding.ASCII.GetBytes(type));
        s.Write(data);
        s.Write(new byte[4]); // CRC — okunmuyor
    }
}
