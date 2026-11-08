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

    [Fact]
    public void Conditional_slots_are_validated()
    {
        Assert.Null(Field(r => r.ConditionalSlots = ["qr-below"]));
        Assert.Equal("conditionalSlots", Field(r => r.ConditionalSlots = Enumerable.Range(0, 33).Select(i => $"slot-{i}").ToList()));
        Assert.Equal("conditionalSlots", Field(r => r.ConditionalSlots = ["qr below"]));
    }

    [Fact]
    public void Static_images_are_validated()
    {
        static StaticImageDto Image(int index = 0) => new() { Index = index, Slot = "seller-top" };

        Assert.Null(Field(r => r.StaticImages = [Image(0), new StaticImageDto { Index = 1, Width = 120, Alignment = "center" }]));
        Assert.Equal("staticImages", Field(r =>
        {
            r.Xslt = null;
            r.ThemeId = Guid.NewGuid();
            r.StaticImages = [Image()];
        }));
        Assert.Equal("staticImages", Field(r => r.StaticImages = Enumerable.Range(0, 21).Select(i => Image(i)).ToList()));
        Assert.Equal("staticImages", Field(r => r.StaticImages = [Image(2), Image(2)]));
        Assert.Equal("staticImages.slot", Field(r => r.StaticImages = [new StaticImageDto { Slot = "seller top" }]));
        Assert.Equal("staticImages.size", Field(r => r.StaticImages = [new StaticImageDto { Width = 9 }]));
        Assert.Equal("staticImages.size", Field(r => r.StaticImages = [new StaticImageDto { Height = 1201 }]));
    }

    [Fact]
    public void Free_images_are_validated()
    {
        static ThemeComposeFreeImageDto Free() => new() { Slot = "seller-top", DataUrl = Png, Width = 150 };

        Assert.Null(Field(r => r.FreeImages = [Free()]));
        Assert.Null(Field(r => r.FreeImages = []));
        Assert.Equal("freeImages", Field(r =>
        {
            r.Xslt = null;
            r.ThemeId = Guid.NewGuid();
            r.FreeImages = [Free()];
        }));
        Assert.Null(Field(r =>
        {
            r.Xslt = null;
            r.ThemeId = Guid.NewGuid();
            r.FreeImages = [];
        }));
        Assert.Equal("freeImages", Field(r => r.FreeImages = [Free(), Free()]));
        Assert.Equal("freeImages", Field(r => r.FreeImages = [null!]));
        Assert.Equal("freeImages", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "seller-top", DataUrl = null! }]));
        Assert.Equal("freeImages.slot", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = null!, DataUrl = Png }]));
        Assert.Equal("freeImages.slot", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "", DataUrl = Png }]));
        Assert.Equal("freeImages.slot", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "seller top", DataUrl = Png }]));
        // existing-* önekle reddedilmez; yer tutucu mu boş kutu mu, yerleşim karar verir.
        Assert.Null(Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "existing-logo", DataUrl = Png }]));
        Assert.Equal("freeImages.size", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "seller-top", DataUrl = Png, Width = 9 }]));
        Assert.Equal("freeImages.size", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "seller-top", DataUrl = Png, Height = 1201 }]));
        Assert.Equal("freeImages.alignment", Field(r => r.FreeImages = [new ThemeComposeFreeImageDto { Slot = "seller-top", DataUrl = Png, Alignment = "justify" }]));
        Assert.Equal("freeImages.dataUrl", Field(r => r.FreeImages =
            [new ThemeComposeFreeImageDto { Slot = "seller-top", DataUrl = new string('A', ThemeImageValidator.MaxDataUrlLength + 1) }]));
    }

    [Fact]
    public void Document_bank_edits_are_validated()
    {
        const string iban = "TR66 0006 7010 0000 0020 1934 37";
        static ThemeComposeDocumentBankEditDto Edit(int? index, string name = "Ziraat", string ibanValue = iban, bool remove = false) =>
            new() { Index = index, BankName = name, Iban = ibanValue, Remove = remove };

        Assert.Null(Field(r => r.DocumentBankEdits = [Edit(0), Edit(1, remove: true, name: "", ibanValue: "")]));
        Assert.Null(Field(r => r.DocumentBankEdits = []));
        Assert.Null(Field(r => r.DocumentBankEdits = [Edit(0, ibanValue: "  TR330006100519786457841326 ")]));
        // Ekleme + dolu bankInfo reddedilir; değiştirme/silme + bankInfo kabul edilir (!canAdd'de önceki blok satırları).
        Assert.Null(Field(r => r.DocumentBankEdits = [Edit(0)]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(null)]));
        Assert.Null(Field(r =>
        {
            r.BankInfo = [new BankInfoItem { BankName = " ", Iban = "" }];
            r.DocumentBankEdits = [Edit(null)];
        }));
        Assert.Equal("documentBankEdits", Field(r =>
        {
            r.Xslt = null;
            r.ThemeId = Guid.NewGuid();
            r.DocumentBankEdits = [Edit(0)];
        }));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [null!]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, name: null!)]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = Enumerable.Range(0, 21).Select(i => Edit(i % 20)).ToList()));
        Assert.Equal("documentBankEdits", Field(r =>
        {
            r.BankInfo = [];
            r.DocumentBankEdits = Enumerable.Range(0, ThemeComposeRequestValidator.MaxBankRows + 1).Select(_ => Edit(null)).ToList();
        }));
        Assert.Equal("documentBankEdits.index", Field(r => r.DocumentBankEdits = [Edit(0), Edit(0, remove: true)]));
        Assert.Equal("documentBankEdits.index", Field(r => r.DocumentBankEdits = [Edit(-1)]));
        Assert.Equal("documentBankEdits.index", Field(r => r.DocumentBankEdits = [Edit(20)]));
        Assert.Equal("documentBankEdits.index", Field(r =>
        {
            r.BankInfo = [];
            r.DocumentBankEdits = [Edit(null, remove: true)];
        }));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, name: new string('a', 101))]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, ibanValue: iban + new string(' ', 30))]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, name: "Ziraat‮")]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, name: "Ziraat\u0007")]));
        Assert.Equal("documentBankEdits", Field(r => r.DocumentBankEdits = [Edit(0, name: "Ziraat￿")]));
        Assert.Equal("documentBankEdits.iban", Field(r => r.DocumentBankEdits = [Edit(0, ibanValue: "TR00 0000")]));
        Assert.Equal("documentBankEdits.iban", Field(r => r.DocumentBankEdits = [Edit(0, ibanValue: "DE89370400440532013000")]));
    }

    private static string? Field(Action<ThemeComposeRequest> mutate)
    {
        var request = Valid();
        mutate(request);
        return ThemeComposeRequestValidator.Validate(request)?.Field;
    }
}
