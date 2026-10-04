using XsltCraft.Application.Imaging;

namespace XsltCraft.Application.Tests.Imaging;

public class ImageHeaderReaderTests
{
    [Fact]
    public void Png_IsDetected_AndDimensionsRead()
    {
        var data = ImageFixtures.Png(1536, 864);

        Assert.Equal(ImageFormat.Png, ImageHeaderReader.DetectFormat(data));
        Assert.True(ImageHeaderReader.TryReadDimensions(data, ImageFormat.Png, out var w, out var h));
        Assert.Equal((1536, 864), (w, h));
    }

    [Fact]
    public void Jpeg_IsDetected_AndSofFoundAfterFillAndDht()
    {
        var data = ImageFixtures.Jpeg(1280, 720);

        Assert.Equal(ImageFormat.Jpeg, ImageHeaderReader.DetectFormat(data));
        Assert.True(ImageHeaderReader.TryReadDimensions(data, ImageFormat.Jpeg, out var w, out var h));
        Assert.Equal((1280, 720), (w, h));
    }

    [Fact]
    public void Jpeg_WithoutSof_ReturnsFalse()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9];
        Assert.False(ImageHeaderReader.TryReadDimensions(data, ImageFormat.Jpeg, out _, out _));
    }

    [Fact]
    public void Jpeg_TruncatedSegment_DoesNotThrow()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xC0, 0x00];
        Assert.False(ImageHeaderReader.TryReadDimensions(data, ImageFormat.Jpeg, out _, out _));
    }

    [Fact]
    public void Png_TruncatedHeader_ReturnsFalse()
    {
        var data = ImageFixtures.Png(10, 10)[..20];
        Assert.False(ImageHeaderReader.TryReadDimensions(data, ImageFormat.Png, out _, out _));
    }

    [Fact]
    public void AnimatedPng_IsDetected()
    {
        Assert.True(ImageHeaderReader.IsAnimatedPng(ImageFixtures.Png(10, 10, animated: true)));
        Assert.False(ImageHeaderReader.IsAnimatedPng(ImageFixtures.Png(10, 10)));
    }

    [Theory]
    [MemberData(nameof(UnsupportedFormats))]
    public void UnsupportedFormats_AreUnknown(byte[] data)
        => Assert.Equal(ImageFormat.Unknown, ImageHeaderReader.DetectFormat(data));

    public static TheoryData<byte[]> UnsupportedFormats() => new()
    {
        ImageFixtures.Gif(),
        ImageFixtures.Svg(),
        ImageFixtures.WebP(),
        Array.Empty<byte>(),
    };
}
