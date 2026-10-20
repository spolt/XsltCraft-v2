using XsltCraft.Application.DTO;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary><c>POST /api/preview/compose</c> biçim doğrulaması: hatalı istek 400 olmalı, 500 değil.</summary>
public class ThemeComposeRequestValidatorTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static ThemeComposeRequest Valid() => new()
    {
        Xslt = "<x/>",
        IncludeXslt = true,
        Assets = [new ThemeComposeAssetDto { Kind = "logo", Slot = "qr-below", DataUrl = Png }],
        BankInfo = [new BankInfoItem { BankName = "Örnek Bankası 😀", Iban = "TR00 0000" }],
        HideThemeImages = ["signature"],
    };

    [Fact]
    public void Valid_request_passes() => Assert.Null(ThemeComposeRequestValidator.Validate(Valid()));

    [Fact]
    public void Null_lists_and_items_are_rejected()
    {
        // JSON'daki "assets": [null] gibi değerler önceden NullReferenceException (500) veriyordu.
        Assert.Equal("assets", Field(r => r.Assets = null!));
        Assert.Equal("assets", Field(r => r.Assets = [null!]));
        Assert.Equal("assets", Field(r => r.Assets[0].DataUrl = null!));
        Assert.Equal("bankInfo", Field(r => r.BankInfo = null!));
        Assert.Equal("bankInfo", Field(r => r.BankInfo = [null!]));
        Assert.Equal("bankInfo", Field(r => r.BankInfo[0].BankName = null!));
        Assert.Equal("bankInfo", Field(r => r.BankInfo[0].Iban = null!));
        Assert.Equal("hideThemeImages", Field(r => r.HideThemeImages = null!));
        Assert.Equal("hideThemeImages", Field(r => r.HideThemeImages = [null!]));
    }

    [Fact]
    public void Characters_that_cannot_be_written_to_xml_are_rejected()
    {
        // Banka tablosu XElement ile serileştirilir; bu karakterler orada hata (500) verirdi.
        string[] invalid = ["Banka￿", "Banka￾", "Banka\uD800", "\uDC00Banka", "Banka\uDC00\uD800", "Banka\u0001"];
        foreach (var value in invalid)
        {
            Assert.Equal("bankInfo", Field(r => r.BankInfo[0].BankName = value));
            Assert.Equal("bankInfo", Field(r => r.BankInfo[0].Iban = value));
        }
    }

    [Fact]
    public void Slot_id_with_trailing_newline_is_invalid()
    {
        // '$' sondaki '\n'i kabul ederdi.
        Assert.DoesNotMatch(ThemeSlotMarkers.SlotIdPattern, "qr-below\n");
        Assert.Matches(ThemeSlotMarkers.SlotIdPattern, "qr-below");
        Assert.Equal("assets.slot", Field(r => r.Assets[0].Slot = "qr-below\n"));
    }

    private static string? Field(Action<ThemeComposeRequest> mutate)
    {
        var request = Valid();
        mutate(request);
        return ThemeComposeRequestValidator.Validate(request)?.Field;
    }
}
