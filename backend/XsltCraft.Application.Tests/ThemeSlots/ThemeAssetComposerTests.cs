using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class ThemeAssetComposerTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly IThemeAssetComposer Composer = new ThemeAssetComposer(new ThemeImageValidator());

    private static readonly Lazy<string> MarkedTheme = new(() =>
    {
        var stripped = new ThemeOverridesProcessor(new ThemeImageValidator())
            .Apply(ThemeFixtures.Load(ThemeFixtures.TestTheme), OverridesDecision.Strip).Xslt;
        return ThemeFixtures.Analyzer().Analyze(stripped, DocumentType.Invoice, []).Xslt;
    });

    private static ThemeComposeInput Input(params ThemeAssetInput[] assets) => new(assets, []);

    [Fact]
    public void Places_logo_into_requested_slot()
    {
        var result = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, "qr-below", Png, 200, 100, ThemeAlignment.Right)));

        Assert.True(result.Success, result.Error);
        Assert.Equal("qr-below", result.AppliedSlots[ThemeAssetKind.Logo]);
        Assert.Contains(@"""slot"":""qr-below""", result.Xslt);
        Assert.Contains(@"data-xc-overrides=""2""", result.Xslt);
        SecureXslt.Compile(result.Xslt);

        var html = ThemeFixtures.Renderer.Render(result.Xslt, ThemeFixtures.Samples.Get(DocumentType.Invoice)).Html;
        Assert.Contains(@"""src"":""" + Png + @"""", html); // JSON verisi çıktıda bozulmadan duruyor
    }

    [Fact]
    public void Compose_is_idempotent_and_replaces_previous_block()
    {
        var logo = new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Png, null, null, ThemeAlignment.Left);
        var once = Composer.Compose(MarkedTheme.Value, Input(logo)).Xslt;
        var twice = Composer.Compose(once, Input(logo)).Xslt;

        Assert.Equal(once, twice);
        Assert.Single(OverridesBlockText.Scan(twice).Blocks);
    }

    [Fact]
    public void Replaces_legacy_v1_block()
    {
        var result = Composer.Compose(ThemeFixtures.Load(ThemeFixtures.TestTheme),
            Input(new ThemeAssetInput(ThemeAssetKind.Signature, null, Png, null, null, ThemeAlignment.Center)));

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain("function setImg(", result.Xslt);
        Assert.Single(OverridesBlockText.Scan(result.Xslt).Blocks);
    }

    [Fact]
    public void Unknown_slot_falls_back_to_catalog_default_then_legacy_ids()
    {
        var defaults = new Dictionary<ThemeAssetKind, string> { [ThemeAssetKind.Logo] = "docinfo-top" };
        var withDefault = Composer.Compose(MarkedTheme.Value, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "yok-boyle-bir-yuva", Png, null, null, ThemeAlignment.Left)], [], defaults));
        Assert.Equal("docinfo-top", withDefault.AppliedSlots[ThemeAssetKind.Logo]);
        Assert.Contains(withDefault.Warnings, w => w.Code == "slot-not-found");

        var legacy = Composer.Compose(ThemeAssetComposerTestsData.UnmarkedTheme,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, "qr-below", Png, null, null, ThemeAlignment.Left)));
        Assert.Null(legacy.AppliedSlots[ThemeAssetKind.Logo]);
        Assert.Contains(legacy.Warnings, w => w.Code == "legacy-fallback");
        Assert.Contains(@"""fb"":[""company_logo"",""companyLogo""]", legacy.Xslt);
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,iVBOR'</script><script>alert(1)//")]
    [InlineData("data:image/png;base64,AAAA")] // PNG değil
    [InlineData(Png + "\n")] // '$' sondaki '\n'i kabul ederdi
    public void Rejects_unsafe_or_invalid_images(string dataUrl)
    {
        var result = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, null, dataUrl, null, null, ThemeAlignment.Left)));

        Assert.False(result.Success);
        Assert.Equal(MarkedTheme.Value, result.Xslt);
    }

    [Fact]
    public void Data_url_with_trailing_newline_is_invalid()
    {
        var validator = new ThemeImageValidator();

        Assert.True(validator.Check(Png).Valid);
        Assert.False(validator.Check(Png + "\n").Valid);
        Assert.False(validator.Check(Png + "\r\n").Valid);
    }

    [Fact]
    public void Bank_info_is_escaped()
    {
        var result = Composer.Compose(MarkedTheme.Value,
            new ThemeComposeInput([], [new BankInfoInput("<script>alert(1)</script>", "TR00 & 1")]));

        Assert.True(result.Success, result.Error);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", result.Xslt);
        Assert.Contains("TR00 &amp; 1", result.Xslt);
        SecureXslt.Compile(result.Xslt);
    }

    [Fact]
    public void No_assets_removes_block()
    {
        var withLogo = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, null, Png, null, null, ThemeAlignment.Left))).Xslt;
        var cleared = Composer.Compose(withLogo, Input());

        Assert.Empty(OverridesBlockText.Scan(cleared.Xslt).Blocks);
    }

    [Fact]
    public void Malformed_markers_fail_closed()
    {
        var broken = MarkedTheme.Value.Replace("</body>", OverridesBlockText.Start + "</body>");
        var result = Composer.Compose(broken,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, null, Png, null, null, ThemeAlignment.Left)));

        Assert.False(result.Success);
    }

    [Fact]
    public void Runtime_script_is_xml_safe()
    {
        var result = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, null, Png, null, null, ThemeAlignment.Left)));
        var start = result.Xslt.IndexOf(@"<script data-xc-overrides=""2"">", StringComparison.Ordinal);
        var code = result.Xslt[(start + 30)..result.Xslt.IndexOf("</script>", start, StringComparison.Ordinal)];

        Assert.DoesNotContain('<', code);
        Assert.DoesNotContain('>', code);
        Assert.DoesNotContain('&', code);
    }

    [Fact]
    public void Large_assets_produce_warning()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ThemeSlots", "Fixtures", "large.png"));
        var dataUrl = "data:image/png;base64," + Convert.ToBase64String(bytes);
        var result = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, null, dataUrl, null, null, ThemeAlignment.Left)));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w.Code == "assets-large");
    }

    private const string Jpeg = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHSEeHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";

    private static string Payload(string dataUrl) => dataUrl[(dataUrl.IndexOf(',') + 1)..];

    private static int Occurrences(string text, string token) =>
        (text.Length - text.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

    [Fact]
    public void Static_image_stays_unchanged_without_static_requests()
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Png, null, null, ThemeAlignment.Left)], []));

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, Occurrences(result.Xslt, Payload(Png)));
        Assert.DoesNotContain(@"""xs""", result.Xslt);
        Assert.DoesNotContain(@"""keep""", result.Xslt);
    }

    [Fact]
    public void Theme_compose_data_keeps_its_shape()
    {
        var result = Composer.Compose(MarkedTheme.Value,
            Input(new ThemeAssetInput(ThemeAssetKind.Logo, "qr-below", Png, 200, 100, ThemeAlignment.Right)));

        Assert.True(result.Success, result.Error);
        var fallback = string.Join(",", LegacyHolderIds.For(ThemeAssetKind.Logo).Select(id => $@"""{id}"""));
        var expected = $$"""{"v":2,"items":[{"k":"logo","slot":"qr-below","fb":[{{fallback}}],"src":"{{Png}}","w":200,"h":100,"a":"right"}],"hide":[]}""";
        Assert.Contains($@"data-xc-overrides-data=""2"">{expected}</script>", result.Xslt);
    }

    [Fact]
    public void Legacy_block_with_extra_slots_and_keep_is_still_recognized()
    {
        var composed = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Jpeg, null, null, ThemeAlignment.Left)], []));
        Assert.True(composed.Success, composed.Error);
        var legacy = composed.Xslt.Replace(@"""a"":""left""}", @"""a"":""left"",""xs"":[""docinfo-top""],""keep"":true}", StringComparison.Ordinal);
        Assert.NotEqual(composed.Xslt, legacy);

        var inspection = new ThemeOverridesProcessor(new ThemeImageValidator()).Inspect(legacy);

        Assert.True(inspection.Recognized, inspection.Reason);
        var image = Assert.Single(inspection.Images);
        Assert.Equal("seller-top", image.Slot);
        Assert.Equal(["docinfo-top"], image.ExtraSlots!);
        Assert.True(image.Keep);
    }

    private static ThemeComposeInput FreeInput(string slot, string dataUrl = Png, params ThemeAssetInput[] assets) =>
        new(assets, [], FreeImages: [new FreeImageInput(slot, dataUrl, 150, null, ThemeAlignment.Center)]);

    [Fact]
    public void Free_image_is_written_without_fallback_and_does_not_hide_theme_logo()
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput("docinfo-top", Jpeg));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.AppliedSlots);
        Assert.True(result.AddedImageBytes > 0);
        var expected = $$"""{"v":2,"items":[{"k":"free","slot":"docinfo-top","fb":[],"src":"{{Jpeg}}","w":150,"h":null,"a":"center"}],"hide":[]}""";
        Assert.Contains($@"data-xc-overrides-data=""2"">{expected}</script>", result.Xslt);
        // Temadaki logo yerinde kalır (statik görsel metinden silinmez).
        Assert.Equal(1, Occurrences(result.Xslt, Payload(Png)));
        SecureXslt.Compile(result.Xslt);
    }

    [Fact]
    public void Free_image_comes_after_assets()
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput("docinfo-top", Jpeg,
            new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Png, null, null, ThemeAlignment.Left)));

        Assert.True(result.Success, result.Error);
        Assert.True(result.Xslt.IndexOf(@"""k"":""logo""", StringComparison.Ordinal)
                    < result.Xslt.IndexOf(@"""k"":""free""", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("yok-boyle-bir-yuva")]
    [InlineData("existing-logo")]
    public void Free_image_with_missing_or_theme_image_slot_is_skipped(string slot)
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput(slot));

        Assert.True(result.Success, result.Error);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("free-slot-not-found", warning.Code);
        Assert.Null(warning.Kind);
        Assert.Empty(OverridesBlockText.Scan(result.Xslt).Blocks);
    }

    [Fact]
    public void Free_image_on_asset_slot_is_occupied()
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput("seller-top", Jpeg,
            new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Png, null, null, ThemeAlignment.Left)));

        Assert.True(result.Success, result.Error);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("slot-occupied", warning.Code);
        Assert.Null(warning.Kind);
        Assert.DoesNotContain(@"""k"":""free""", result.Xslt);
    }

    [Fact]
    public void Free_image_on_static_image_box_is_occupied()
    {
        var theme = ThemeAssetComposerTestsData.PlaceholderTheme(Png);
        var result = Composer.Compose(theme, new ThemeComposeInput([], [],
            StaticImages: [new StaticImageInput(0, "docinfo-top", false, null, null, null)],
            FreeImages: [new FreeImageInput("docinfo-top", Jpeg, null, null, ThemeAlignment.Left)]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w is { Code: "slot-occupied", Kind: null });
        Assert.DoesNotContain(@"""k"":""free""", result.Xslt);
    }

    [Fact]
    public void Free_image_on_conditional_slot_warns_and_is_placed()
    {
        var result = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput("docinfo-top") with
        {
            ConditionalSlots = new HashSet<string> { "docinfo-top" },
        });

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w is { Code: "slot-conditional", Kind: null });
        Assert.Contains(@"""k"":""free""", result.Xslt);
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Invalid_free_image_fails_closed(string dataUrl)
    {
        var theme = ThemeAssetComposerTestsData.PlaceholderTheme(Png);
        var result = Composer.Compose(theme, FreeInput("yok-boyle-bir-yuva", dataUrl));

        Assert.False(result.Success);
        Assert.Equal(theme, result.Xslt);
    }

    [Fact]
    public void Free_image_round_trips_and_is_not_keepable()
    {
        var composed = Composer.Compose(ThemeAssetComposerTestsData.PlaceholderTheme(Png), FreeInput("docinfo-top", Jpeg));
        Assert.True(composed.Success, composed.Error);

        var inspection = new ThemeOverridesProcessor(new ThemeImageValidator()).Inspect(composed.Xslt);

        Assert.True(inspection.Recognized, inspection.Reason);
        Assert.False(inspection.Keepable);
        Assert.Empty(inspection.Images);
        var free = Assert.Single(inspection.FreeImages!);
        Assert.Equal(new OverridesFreeImageInfo("docinfo-top", Jpeg, 150, null, "center", true, null), free);
    }
}

internal static class ThemeAssetComposerTestsData
{
    public static string PlaceholderTheme(string imageSrc) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="html"/>
          <xsl:template match="/"><html><body>
            <div data-xc-slot="seller-top"></div>
            <img data-xc-slot="existing-logo" data-xc-placeholder="logo" src="{imageSrc}"/>
            <div data-xc-slot="docinfo-top"></div>
          </body></html></xsl:template>
        </xsl:stylesheet>
        """;

    public const string UnmarkedTheme = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="html"/>
          <xsl:template match="/"><html><body><div id="company_logo"></div></body></html></xsl:template>
        </xsl:stylesheet>
        """;
}
