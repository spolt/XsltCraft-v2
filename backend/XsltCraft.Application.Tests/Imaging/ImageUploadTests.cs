using System.Text;

using XsltCraft.Application.Imaging;

namespace XsltCraft.Application.Tests.Imaging;

public class ImageUploadTests
{
    [Theory]
    [InlineData("logo.png", "image/png", ".png")]
    [InlineData("LOGO.PNG", "image/png", ".png")]
    public void Png_IsAccepted_WithCanonicalMimeAndExtension(string name, string mime, string ext)
    {
        var r = ImageUpload.Validate(ImageFixtures.Png(300, 100), name);

        Assert.True(r.IsValid);
        Assert.Equal((mime, ext, 300, 100), (r.MimeType, r.Extension, r.Width, r.Height));
    }

    [Theory]
    [InlineData("imza.jpg")]
    [InlineData("imza.jpeg")]
    public void Jpeg_IsAccepted_NormalizedToJpg(string name)
    {
        var r = ImageUpload.Validate(ImageFixtures.Jpeg(640, 480), name);

        Assert.True(r.IsValid);
        Assert.Equal(("image/jpeg", ".jpg"), (r.MimeType, r.Extension));
    }

    [Theory]
    [InlineData("logo.svg")]
    [InlineData("logo.gif")]
    [InlineData("logo.webp")]
    [InlineData("logo.html")]
    [InlineData("logo")]
    [InlineData(null)]
    public void DisallowedExtension_IsRejected(string? name)
        => Assert.False(ImageUpload.Validate(ImageFixtures.Png(10, 10), name).IsValid);

    [Fact]
    public void SvgContent_RenamedToPng_IsRejected()
    {
        // Saldırı: SVG/HTML içerik ".png" adıyla → eskiden istemci MIME'ıyla aynı origin'den servis ediliyordu.
        var r = ImageUpload.Validate(ImageFixtures.Svg(), "logo.png");

        Assert.False(r.IsValid);
        Assert.Equal(ImageUpload.TypeMessage, r.Error);
    }

    [Fact]
    public void HtmlContent_RenamedToJpg_IsRejected()
        => Assert.False(ImageUpload.Validate(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), "x.jpg").IsValid);

    [Fact]
    public void JpegContent_WithPngExtension_IsMismatch()
    {
        var r = ImageUpload.Validate(ImageFixtures.Jpeg(10, 10), "logo.png");

        Assert.False(r.IsValid);
        Assert.Contains("uyuşmuyor", r.Error);
    }

    [Fact]
    public void HugeDimensions_AreRejected()
        => Assert.False(ImageUpload.Validate(ImageFixtures.Png(50_000, 50_000), "bomb.png").IsValid);

    [Fact]
    public void Empty_IsRejected()
        => Assert.False(ImageUpload.Validate([], "a.png").IsValid);

    [Theory]
    [InlineData("assets/u/a.png", "image/png")]
    [InlineData("assets/u/a.jpg", "image/jpeg")]
    [InlineData("assets/u/a.JPEG", "image/jpeg")]
    [InlineData("assets/u/a.svg", "image/svg+xml")]        // eski kayıt — sandbox CSP ile servis edilir
    [InlineData("assets/u/a.html", "application/octet-stream")]
    [InlineData("assets/u/a", "application/octet-stream")]
    public void ServeMimeType_DerivedFromStoredExtension(string path, string expected)
        => Assert.Equal(expected, ImageUpload.ServeMimeType(path));
}
