using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;
using XsltCraft.Application.Tests.Imaging;

namespace XsltCraft.Application.Tests.Ai.Vision;

public class AiImageValidatorTests
{
    private static readonly VisionOptions Options = new();
    private readonly AiImageValidator _sut = new(Options);

    private static AiImagePayload Png(int w = 800, int h = 600) => new("image/png", Convert.ToBase64String(ImageFixtures.Png(w, h)));
    private static AiImagePayload Jpeg(int w = 800, int h = 600) => new("image/jpeg", Convert.ToBase64String(ImageFixtures.Jpeg(w, h)));

    [Fact]
    public void ValidPngAndJpeg_AreAccepted_WithDimensions()
    {
        var result = _sut.Validate([Png(1536, 864), Jpeg(1280, 720)], maxCount: 3);

        Assert.True(result.IsValid);
        Assert.Collection(result.Images,
            i => Assert.Equal(("image/png", 1536, 864), (i.MimeType, i.Width, i.Height)),
            i => Assert.Equal(("image/jpeg", 1280, 720), (i.MimeType, i.Width, i.Height)));
    }

    [Fact]
    public void JpgAlias_IsNormalizedToJpeg()
    {
        var result = _sut.Validate([Jpeg() with { MimeType = "image/jpg" }], 3);

        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.Images[0].MimeType);
    }

    [Fact]
    public void EmptyList_IsValid()
        => Assert.True(_sut.Validate([], 1).IsValid);

    [Fact]
    public void MoreThanMaxCount_IsRejected()
        => AssertError(_sut.Validate([Png(), Png()], maxCount: 1), AiImageValidator.CodeCount);

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("image/webp")]
    [InlineData("text/html")]
    public void UnsupportedDeclaredMime_IsRejected(string mime)
        => AssertError(_sut.Validate([Png() with { MimeType = mime }], 3), AiImageValidator.CodeType);

    [Fact]
    public void SvgContent_DeclaredAsPng_IsRejected()
    {
        var svg = new AiImagePayload("image/png", Convert.ToBase64String(ImageFixtures.Svg()));
        AssertError(_sut.Validate([svg], 3), AiImageValidator.CodeType);
    }

    [Fact]
    public void JpegContent_DeclaredAsPng_IsMimeMismatch()
    {
        var spoofed = Jpeg() with { MimeType = "image/png" };
        AssertError(_sut.Validate([spoofed], 3), AiImageValidator.CodeMimeMismatch);
    }

    [Fact]
    public void AnimatedPng_IsRejected()
    {
        var apng = new AiImagePayload("image/png", Convert.ToBase64String(ImageFixtures.Png(10, 10, animated: true)));
        AssertError(_sut.Validate([apng], 3), AiImageValidator.CodeType);
    }

    [Fact]
    public void DecompressionBomb_HeaderDeclaresHugeDimensions_IsRejectedWithoutDecode()
        => AssertError(_sut.Validate([Png(50_000, 50_000)], 3), AiImageValidator.CodeDimensions);

    [Fact]
    public void LongEdgeOverLimit_IsRejected()
        => AssertError(_sut.Validate([Png(Options.MaxLongEdgePx + 1, 100)], 3), AiImageValidator.CodeDimensions);

    [Fact]
    public void InvalidBase64_IsRejected()
        => AssertError(_sut.Validate([new AiImagePayload("image/png", "not*base64!")], 3), AiImageValidator.CodeInvalid);

    [Fact]
    public void DataUrlPrefix_IsRejected()
    {
        var payload = new AiImagePayload("image/png", "data:image/png;base64," + Png().Data);
        AssertError(_sut.Validate([payload], 3), AiImageValidator.CodeInvalid);
    }

    [Theory]
    [InlineData(null, "AAAA")]
    [InlineData("image/png", null)]
    [InlineData("image/png", "")]
    public void MissingFields_AreRejected(string? mime, string? data)
        => AssertError(_sut.Validate([new AiImagePayload(mime, data)], 3), AiImageValidator.CodeInvalid);

    [Fact]
    public void OversizedSingleImage_IsRejectedBeforeDecode()
    {
        var huge = new string('A', (Options.MaxImageBytes + 1024) / 3 * 4);
        AssertError(_sut.Validate([new AiImagePayload("image/png", huge)], 3), AiImageValidator.CodeTooLarge);
    }

    [Fact]
    public void TotalOverLimit_IsRejected()
    {
        var sut = new AiImageValidator(new VisionOptions { MaxImageBytes = 1_000_000, MaxTotalBytes = 1_000 });
        var big = PaddedPng(600);

        AssertError(sut.Validate([big, big], 3), AiImageValidator.CodeTooLarge);
    }

    [Fact]
    public void Base64WithLineBreaks_IsCanonicalized()
    {
        var canonical = Png().Data!;
        var wrapped = canonical[..8] + "\r\n" + canonical[8..];

        var result = _sut.Validate([new AiImagePayload("image/png", wrapped)], 3);

        Assert.True(result.IsValid);
        Assert.Equal(canonical, result.Images[0].Base64);
    }

    private static AiImagePayload PaddedPng(int totalBytes)
    {
        var png = ImageFixtures.Png(10, 10);
        var padded = new byte[Math.Max(totalBytes, png.Length)];
        png.CopyTo(padded, 0);
        return new("image/png", Convert.ToBase64String(padded));
    }

    private static void AssertError(AiImageValidationResult result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Empty(result.Images);
    }
}
