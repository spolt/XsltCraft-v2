using System.Xml.Linq;

using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>Dosyadaki görsellerin metinde statik taşınması (compose xslt dalı).</summary>
public class StaticImageMoverTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    private const string Jpeg = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHSEeHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";

    private static readonly IThemeAssetComposer Composer = new ThemeAssetComposer(new ThemeImageValidator());

    // Görsel 0: logo (Png), görsel 1: imza (Jpeg).
    private static readonly string Theme = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" exclude-result-prefixes="n1 cbc">
          <xsl:output method="html"/>
          <xsl:template match="/"><html><body>
            <table><tr>
              <td id="seller"><div data-xc-slot="seller-top" data-xc-layout="block-clear" data-xc-inserted="1"></div><xsl:value-of select="//cbc:ID"/></td>
              <td id="logo-cell"><img data-xc-slot="existing-logo" data-xc-placeholder="logo" id="firma" style="float:right;width:120px" src="{Png}"/></td>
            </tr></table>
            <div id="notes" data-xc-slot="notes-box"></div>
            <img data-xc-slot="existing-signature" data-xc-placeholder="signature" src="{Jpeg}"/>
          </body></html></xsl:template>
        </xsl:stylesheet>
        """;

    private static ThemeComposeResult Compose(string xslt, params StaticImageInput[] images) =>
        Composer.Compose(xslt, new ThemeComposeInput([], [], StaticImages: images));

    private static StaticImageInput Move(int index, string? slot = null, bool remove = false,
        int? width = null, int? height = null, ThemeAlignment? alignment = null) => new(index, slot, remove, width, height, alignment);

    private static IUploadedXsltAnalyzer EasyEditAnalyzer() =>
        new UploadedXsltAnalyzer(new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(new ThemeImageValidator()), ThemeFixtures.Analyzer());

    private static int Occurrences(string text, string dataUrl)
    {
        var token = dataUrl[(dataUrl.IndexOf(',') + 1)..];
        return (text.Length - text.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;
    }

    private static List<XElement> Images(string xslt, string src) =>
        XDocument.Parse(xslt).Descendants("img").Where(e => (string?)e.Attribute("src") == src).ToList();

    private static void AssertSafe(string xslt)
    {
        Assert.Null(XsltSafety.FindThreat(xslt));
        Assert.Contains(@"version=""2.0""", xslt);
        Assert.DoesNotContain("<!--", xslt);
        SecureXslt.Compile(xslt);
    }

    [Fact]
    public void Move_places_image_in_target_slot_once()
    {
        var result = Compose(Theme, Move(0, "seller-top"));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, Occurrences(result.Xslt, Png));
        var img = Assert.Single(Images(result.Xslt, Png));
        Assert.Equal("seller", (string?)img.Ancestors("td").First().Attribute("id"));
        Assert.Equal("logo", (string?)img.Attribute(ThemeSlotMarkers.PlaceholderAttribute));
        Assert.DoesNotContain("display:none", result.Xslt);
        Assert.DoesNotContain("data-xc-overrides", result.Xslt);
        AssertSafe(result.Xslt);
    }

    [Fact]
    public void Move_onto_theme_image_takes_its_place()
    {
        var result = Compose(Theme, Move(1, "existing-logo"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(0, Occurrences(result.Xslt, Png));
        Assert.Equal(1, Occurrences(result.Xslt, Jpeg));
        var img = Assert.Single(Images(result.Xslt, Jpeg));
        Assert.Equal("existing-logo", (string?)img.Attribute(ThemeSlotMarkers.SlotAttribute));
        Assert.Equal("signature", (string?)img.Attribute(ThemeSlotMarkers.PlaceholderAttribute));
        AssertSafe(result.Xslt);
    }

    [Fact]
    public void Missing_target_slot_keeps_image_in_place()
    {
        var result = Compose(Theme, Move(0, "yok-boyle"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(Theme, result.Xslt);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("slot-not-found", warning.Code);
        Assert.Equal(ThemeAssetKind.Logo, warning.Kind);
    }

    [Fact]
    public void Remove_deletes_image_markup()
    {
        var result = Compose(Theme, Move(1, remove: true));

        Assert.True(result.Success, result.Error);
        Assert.Equal(0, Occurrences(result.Xslt, Jpeg));
        Assert.Equal(1, Occurrences(result.Xslt, Png));
        AssertSafe(result.Xslt);
    }

    [Fact]
    public void Size_and_alignment_are_written_to_style()
    {
        var result = Compose(Theme, Move(0, width: 200, height: 80, alignment: ThemeAlignment.Center));

        Assert.True(result.Success, result.Error);
        var style = (string?)Assert.Single(Images(result.Xslt, Png)).Attribute("style");
        Assert.Equal("float:right;object-fit:contain;object-position:center center;width:200px;height:80px", style);
    }

    [Fact]
    public void Requests_for_kind_with_new_asset_are_ignored()
    {
        var result = Composer.Compose(Theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Jpeg, null, null, ThemeAlignment.Left)], [],
            StaticImages: [Move(0, "notes-box")]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w.Code == "static-image-ignored" && w.Kind == ThemeAssetKind.Logo);
        Assert.Equal("existing-logo", (string?)Assert.Single(Images(result.Xslt, Png)).Attribute(ThemeSlotMarkers.SlotAttribute));
    }

    [Fact]
    public async Task Moved_image_role_is_read_back_from_marker()
    {
        var marked = (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)], Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;
        var slots = ThemeSlotMarkers.SlotIdsIn(marked);
        var target = slots.First(s => s.Contains("notes", StringComparison.Ordinal) || s.Contains("total", StringComparison.Ordinal));

        var result = Compose(marked, Move(0, target));
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.NotEqual(marked, result.Xslt);

        var analysis = await EasyEditAnalyzer().AnalyzeAsync(result.Xslt, null, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(2, analysis.Images.Count);
        Assert.All(analysis.Images, i => Assert.Equal(EasyEditRoleSource.Marker, i.Source));
        Assert.Equal([ThemeImageRole.Signature, ThemeImageRole.Logo], analysis.Images.Select(i => i.Role));
    }

    [Fact]
    public void Move_into_adjacent_inserted_marker_succeeds()
    {
        var theme = Theme.Replace(@"<td id=""logo-cell""><img", @"<td id=""logo-cell""><div data-xc-slot=""logo-top"" data-xc-layout=""block"" data-xc-inserted=""1""></div><img");

        var result = Compose(theme, Move(0, "logo-top"));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, Occurrences(result.Xslt, Png));
        var img = Assert.Single(Images(result.Xslt, Png));
        Assert.Equal("logo-cell", (string?)img.Ancestors("td").First().Attribute("id"));
        AssertSafe(result.Xslt);
    }

    [Fact]
    public void Non_movable_image_move_request_warns_and_keeps_text()
    {
        const string svg = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=";
        var theme = Theme.Replace("</body>", $@"<img id=""extra"" style=""width:60px"" src=""{svg}""/></body>");

        var result = Compose(theme, Move(2, "seller-top"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(theme, result.Xslt);
        Assert.Equal("static-image-not-editable", Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public void Asset_targeting_box_filled_by_static_image_is_skipped()
    {
        var result = Composer.Compose(Theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "notes-box", Jpeg, null, null, ThemeAlignment.Left)], [],
            StaticImages: [Move(1, "notes-box")]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w.Code == "slot-occupied" && w.Kind == ThemeAssetKind.Logo);
        Assert.DoesNotContain("data-xc-overrides", result.Xslt);
        var img = Assert.Single(Images(result.Xslt, Jpeg));
        Assert.Equal("notes", (string?)img.Ancestors("div").First(d => d.Attribute("id") is not null).Attribute("id"));
    }

    [Fact]
    public void Moving_twice_leaves_no_empty_wrapper()
    {
        var first = Compose(Theme, Move(0, "notes-box"));
        Assert.True(first.Success, first.Error);
        Assert.Contains(StaticImageMarkup.WrapAttribute, first.Xslt);

        var second = Compose(first.Xslt, Move(0, "seller-top"));

        Assert.True(second.Success, second.Error);
        Assert.Empty(second.Warnings);
        Assert.Equal(1, Occurrences(second.Xslt, Png));
        var wrappers = XDocument.Parse(second.Xslt).Descendants("div").Where(d => d.Attribute(StaticImageMarkup.WrapAttribute) is not null).ToList();
        Assert.All(wrappers, w => Assert.True(w.HasElements));
        Assert.Empty(XDocument.Parse(second.Xslt).Descendants("div").Single(d => (string?)d.Attribute("id") == "notes").Elements());
        AssertSafe(second.Xslt);
    }

    [Fact]
    public void Move_into_inserted_marker_fills_it_and_cleans_to_plain_wrapper()
    {
        var result = Compose(Theme, Move(0, "seller-top"));

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(@"data-xc-slot=""seller-top"" data-xc-layout", result.Xslt);
        var wrapper = XDocument.Parse(result.Xslt).Descendants("div")
            .Single(d => (string?)d.Attribute(ThemeSlotMarkers.SlotAttribute) == "seller-top");
        Assert.NotNull(wrapper.Attribute(StaticImageMarkup.WrapAttribute));
        Assert.Null(wrapper.Attribute("data-xc-inserted"));
        Assert.Equal("logo", (string?)Assert.Single(wrapper.Elements("img")).Attribute(ThemeSlotMarkers.PlaceholderAttribute));
        AssertSafe(result.Xslt);

        var cleaned = ThemeSlotMarkers.Clean(result.Xslt);
        Assert.DoesNotContain("data-xc-inserted", cleaned);
        Assert.DoesNotContain(ThemeSlotMarkers.SlotAttribute, cleaned);
        var plain = XDocument.Parse(cleaned).Descendants("div").Single(d => d.Attribute(StaticImageMarkup.WrapAttribute) is not null);
        Assert.Single(plain.Elements("img"));
        Assert.Equal("seller", (string?)plain.Parent?.Attribute("id"));
    }

    [Fact]
    public void Asset_targeting_inserted_marker_filled_by_static_image_is_skipped()
    {
        var result = Composer.Compose(Theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Signature, "seller-top", Png, null, null, ThemeAlignment.Left)], [],
            StaticImages: [Move(0, "seller-top")]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Warnings, w => w.Code == ThemeAssetSlotRules.SlotOccupiedCode && w.Kind == ThemeAssetKind.Signature);
        Assert.DoesNotContain("data-xc-overrides", result.Xslt);
        Assert.Null(result.AppliedSlots[ThemeAssetKind.Signature]);
    }

    [Fact]
    public async Task Image_moved_into_inserted_marker_keeps_marker_role_on_reanalysis()
    {
        var marked = (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)], Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;
        var inserted = XDocument.Parse(marked).Descendants()
            .Where(e => e.Attribute("data-xc-inserted") is not null)
            .Select(e => (string)e.Attribute(ThemeSlotMarkers.SlotAttribute)!)
            .ToList();
        Assert.NotEmpty(inserted);

        var result = Compose(marked, Move(1, inserted[^1]));
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain($@"data-xc-slot=""{inserted[^1]}"" data-xc-layout", result.Xslt);

        var analysis = await EasyEditAnalyzer().AnalyzeAsync(result.Xslt, null, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(2, analysis.Images.Count);
        Assert.All(analysis.Images, i => Assert.Equal(EasyEditRoleSource.Marker, i.Source));
        Assert.Contains(analysis.Images, i => i.Role == ThemeImageRole.Signature);
        Assert.Contains(analysis.Images, i => i.Role == ThemeImageRole.Logo);
    }

    [Fact]
    public void Move_into_slot_group_above_image_limit_warns_and_keeps_text()
    {
        var group = string.Concat(Enumerable.Repeat($@"<img data-xc-slot=""stamp-row"" style=""width:40px"" src=""{Png}""/>", StaticImagePlan.MaxGroupImages + 1));
        var theme = Theme.Replace("</body>", $@"<div id=""stamps"">{group}</div></body>");

        var result = Compose(theme, Move(1, "stamp-row"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(theme, result.Xslt);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(StaticImageWarnings.TooLarge, warning.Code);
        Assert.Equal(ThemeAssetKind.Signature, warning.Kind);
    }

    [Fact]
    public void Compose_output_above_request_limit_fails_with_too_large_code()
    {
        var padding = ThemeComposeRequestValidator.MaxXsltBytes - System.Text.Encoding.UTF8.GetByteCount(Theme) - 64;
        var theme = Theme.Replace("</body>", $"<p>{new string('x', padding)}</p></body>");
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(theme) <= ThemeComposeRequestValidator.MaxXsltBytes);

        var result = Composer.Compose(theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "seller-top", Jpeg, null, null, ThemeAlignment.Left)], []));

        Assert.False(result.Success);
        Assert.Equal(ThemeAssetComposer.XsltTooLargeCode, result.ErrorCode);
        Assert.Equal("xslt-too-large", result.ErrorCode);
    }

    private const string VacatedLogoBox = @"<div data-xc-slot=""existing-logo"" data-xc-layout=""block"" data-xc-inserted=""1""></div>";

    private static XElement Cell(string xslt, string id) =>
        XDocument.Parse(xslt).Descendants("td").Single(td => (string?)td.Attribute("id") == id);

    [Fact]
    public void Moved_image_leaves_empty_marker_box_and_clean_removes_it()
    {
        var result = Compose(Theme, Move(0, "seller-top"));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Contains($@"<td id=""logo-cell"">{VacatedLogoBox}</td>", result.Xslt);
        Assert.Equal(1, ThemeSlotMarkers.CountOccurrences(result.Xslt, "existing-logo"));
        Assert.Null(Assert.Single(Images(result.Xslt, Png)).Attribute(ThemeSlotMarkers.SlotAttribute));
        Assert.Contains("existing-logo", ThemeSlotMarkers.EmptyInsertedSlotIdsIn(result.Xslt));
        AssertSafe(result.Xslt);

        var cleaned = ThemeSlotMarkers.Clean(result.Xslt);
        Assert.DoesNotContain("data-xc-inserted", cleaned);
        Assert.DoesNotContain(ThemeSlotMarkers.SlotAttribute, cleaned);
        Assert.Empty(Cell(cleaned, "logo-cell").Nodes());
        Assert.Equal(2, XDocument.Parse(cleaned).Descendants("img").Count());
    }

    [Fact]
    public void Removed_image_leaves_empty_marker_box()
    {
        var result = Compose(Theme, Move(0, remove: true));

        Assert.True(result.Success, result.Error);
        Assert.Equal(0, Occurrences(result.Xslt, Png));
        Assert.Contains($@"<td id=""logo-cell"">{VacatedLogoBox}</td>", result.Xslt);
        Assert.Empty(Cell(ThemeSlotMarkers.Clean(result.Xslt), "logo-cell").Nodes());
        AssertSafe(result.Xslt);
    }

    [Fact]
    public void Moved_image_with_static_wrapper_leaves_box_in_wrapper_place()
    {
        var theme = Theme.Replace(
            $@"<td id=""logo-cell""><img data-xc-slot=""existing-logo"" data-xc-placeholder=""logo"" id=""firma"" style=""float:right;width:120px"" src=""{Png}""/></td>",
            $@"<td id=""logo-cell""><div data-xc-static-wrap=""1"" style=""display:flex""><img data-xc-slot=""existing-logo"" data-xc-placeholder=""logo"" src=""{Png}""/></div></td>");
        Assert.NotEqual(Theme, theme);

        var result = Compose(theme, Move(0, "notes-box"));

        Assert.True(result.Success, result.Error);
        Assert.Contains($@"<td id=""logo-cell"">{VacatedLogoBox}</td>", result.Xslt);
        AssertSafe(result.Xslt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Other_kind_moved_into_vacated_box_fills_it(bool otherFirst)
    {
        StaticImageInput[] requests = otherFirst
            ? [Move(1, "existing-logo"), Move(0, "seller-top")]
            : [Move(0, "seller-top"), Move(1, "existing-logo")];

        var result = Compose(Theme, requests);

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        var wrapper = Assert.Single(Cell(result.Xslt, "logo-cell").Elements());
        Assert.Equal("existing-logo", (string?)wrapper.Attribute(ThemeSlotMarkers.SlotAttribute));
        Assert.NotNull(wrapper.Attribute(StaticImageMarkup.WrapAttribute));
        var signature = Assert.Single(wrapper.Elements("img"));
        Assert.Equal(Jpeg, (string?)signature.Attribute("src"));
        Assert.Equal("signature", (string?)signature.Attribute(ThemeSlotMarkers.PlaceholderAttribute));
        Assert.Null(signature.Attribute(ThemeSlotMarkers.SlotAttribute));
        Assert.Equal(["existing-signature"], ThemeSlotMarkers.EmptyInsertedSlotIdsIn(result.Xslt));
        AssertSafe(result.Xslt);

        var cleaned = ThemeSlotMarkers.Clean(result.Xslt);
        Assert.DoesNotContain(ThemeSlotMarkers.SlotAttribute, cleaned);
        Assert.DoesNotContain("data-xc-inserted", cleaned);
        Assert.Equal(2, XDocument.Parse(cleaned).Descendants("img").Count());
    }

    [Fact]
    public void Asset_targets_vacated_box_and_is_skipped_when_box_is_filled()
    {
        var empty = Composer.Compose(Theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Signature, "existing-logo", Jpeg, null, null, ThemeAlignment.Left)], [],
            StaticImages: [Move(0, "seller-top")]));
        Assert.True(empty.Success, empty.Error);
        Assert.Empty(empty.Warnings);
        Assert.Equal("existing-logo", empty.AppliedSlots[ThemeAssetKind.Signature]);
        Assert.Contains(@"""slot"":""existing-logo""", empty.Xslt);

        var filled = Composer.Compose(Theme, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "existing-signature", Png, null, null, ThemeAlignment.Left)], [],
            StaticImages: [Move(1, "notes-box")]));
        Assert.True(filled.Success, filled.Error);
        Assert.Equal("existing-signature", filled.AppliedSlots[ThemeAssetKind.Logo]);
    }

    [Fact]
    public void Free_image_goes_into_vacated_box_but_not_onto_placeholder()
    {
        FreeImageInput[] free = [new("existing-logo", Jpeg, 150, null, ThemeAlignment.Left)];

        var vacated = Composer.Compose(Theme, new ThemeComposeInput([], [], StaticImages: [Move(0, "seller-top")], FreeImages: free));
        Assert.True(vacated.Success, vacated.Error);
        Assert.Empty(vacated.Warnings);
        Assert.Contains(@"""k"":""free"",""slot"":""existing-logo""", vacated.Xslt);
        Assert.Null(XsltSafety.FindThreat(vacated.Xslt));
        SecureXslt.Compile(vacated.Xslt);

        var placeholder = Composer.Compose(Theme, new ThemeComposeInput([], [], FreeImages: free));
        Assert.True(placeholder.Success, placeholder.Error);
        Assert.Equal("free-slot-not-found", Assert.Single(placeholder.Warnings).Code);
        Assert.DoesNotContain(@"""k"":""free""", placeholder.Xslt);

        var filled = Composer.Compose(Theme, new ThemeComposeInput([], [],
            StaticImages: [Move(0, "seller-top"), Move(1, "existing-logo")], FreeImages: free));
        Assert.True(filled.Success, filled.Error);
        Assert.Contains(filled.Warnings, w => w is { Code: "slot-occupied", Kind: null });
        Assert.DoesNotContain(@"""k"":""free""", filled.Xslt);
    }

    [Fact]
    public async Task Image_moved_into_vacated_box_keeps_roles_on_reanalysis()
    {
        var marked = (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)], Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;
        var slots = ThemeSlotMarkers.SlotIdsIn(marked);
        Assert.Contains("existing-logo", slots);
        var target = slots.First(s => s.Contains("notes", StringComparison.Ordinal) || s.Contains("total", StringComparison.Ordinal));

        var result = Compose(marked, Move(0, target), Move(1, "existing-logo"));
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Contains("existing-signature", ThemeSlotMarkers.EmptyInsertedSlotIdsIn(result.Xslt));

        var analysis = await EasyEditAnalyzer().AnalyzeAsync(result.Xslt, null, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(2, analysis.Images.Count);
        Assert.All(analysis.Images, i => Assert.Equal(EasyEditRoleSource.Marker, i.Source));
        Assert.Contains(analysis.Images, i => i.Role == ThemeImageRole.Signature);
        Assert.Contains(analysis.Images, i => i.Role == ThemeImageRole.Logo);
    }

    // İstemci sırası (logo, imza): logo imzanın yerine, imza başka yuvaya; sonra imza ikinci kez başka yuvaya.
    [Theory]
    [InlineData("seller-top")]
    [InlineData("notes-box")]
    public void Logo_into_signature_place_while_signature_moves_elsewhere_succeeds(string signatureTarget)
    {
        var result = Compose(Theme, Move(0, "existing-signature"), Move(1, signatureTarget));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, Occurrences(result.Xslt, Png));
        Assert.Equal(1, Occurrences(result.Xslt, Jpeg));
        var doc = XDocument.Parse(result.Xslt);
        var logo = Assert.Single(Images(result.Xslt, Png));
        Assert.Equal("existing-signature", (string?)logo.Parent!.Attribute(ThemeSlotMarkers.SlotAttribute));
        Assert.Equal("logo", (string?)logo.Attribute(ThemeSlotMarkers.PlaceholderAttribute));
        var signature = Assert.Single(Images(result.Xslt, Jpeg));
        Assert.Equal(signatureTarget, signature.Ancestors().Select(a => (string?)a.Attribute(ThemeSlotMarkers.SlotAttribute)).First(s => s is not null));
        Assert.Contains("existing-logo", ThemeSlotMarkers.EmptyInsertedSlotIdsIn(result.Xslt));
        Assert.Equal(1, ThemeSlotMarkers.CountOccurrences(result.Xslt, "existing-signature"));
        Assert.Single(doc.Descendants(), e => (string?)e.Attribute(ThemeSlotMarkers.SlotAttribute) == "existing-signature");
        AssertSafe(result.Xslt);
    }
}
