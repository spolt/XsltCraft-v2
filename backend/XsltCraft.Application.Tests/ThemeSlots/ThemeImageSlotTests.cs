using System.Text.RegularExpressions;

using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Validation;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>Temaya gömülü statik görseller: tespit, rol önerisi, yuva olarak işaretleme ve yükleme kararı.</summary>
public class ThemeImageSlotTests
{
    private static readonly IThemeSlotAnalyzer Analyzer = ThemeFixtures.Analyzer();
    private static readonly ThemeImageDecision[] LogoAndStamp = [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)];
    private static ThemeImageDecision Design => new(ThemeImageRole.Design, false);

    private static readonly IThemeUploadProcessor Processor =
        new ThemeUploadProcessor(new ThemeOverridesProcessor(new ThemeImageValidator()), Analyzer, ThemeFixtures.Renderer, ThemeFixtures.Samples);

    [Fact]
    public void Detects_company_logo_and_stamp_but_not_gib_logo_or_barcode()
    {
        var images = Analyzer.DetectImages(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice);

        Assert.Equal(2, images.Count);
        Assert.Equal(ThemeImageRole.Logo, images[0].Suggested);      // başlık tablosunda, karekodun yanında
        Assert.Equal(ThemeImageRole.Signature, images[1].Suggested); // alıcı satırında (alt="company_logo" olsa da)
        Assert.All(images, i => Assert.StartsWith("data:image/png;base64,", i.PreviewDataUrl));
        Assert.Equal([0, 1], images.Select(i => i.Index));
    }

    [Fact]
    public void Gib_fixtures_have_no_slottable_images()
    {
        foreach (var (name, type) in ThemeFixtures.All.Where(f => f.Name != ThemeFixtures.TestTheme))
            Assert.Empty(Analyzer.DetectImages(ThemeFixtures.Load(name), type));
    }

    [Fact]
    public void Marked_images_become_default_slots_and_render_identically()
    {
        var source = ThemeFixtures.CompanyImagesTheme;
        var result = Analyzer.Analyze(source, DocumentType.Invoice, [], LogoAndStamp);

        Assert.NotEqual(ThemeSlotStatus.Failed, result.Catalog.Status);
        var slots = result.Catalog.Slots.ToDictionary(s => s.Id);
        Assert.Equal(ThemeAssetKind.Logo, slots["existing-logo"].DefaultFor);
        Assert.Equal(ThemeAssetKind.Signature, slots["existing-signature"].DefaultFor);
        Assert.False(slots["existing-logo"].Inserted);
        Assert.True(slots["existing-logo"].Removable);
        Assert.False(slots["existing-signature"].Removable);

        Assert.Contains(@"<img data-xc-slot=""existing-logo"" data-xc-placeholder=""logo"" data-xc-removable=""1"" style=""float:right;", result.Xslt);
        Assert.Contains(@"<img data-xc-slot=""existing-signature"" data-xc-placeholder=""signature"" src=", result.Xslt);

        Assert.Null(XsltSafety.FindThreat(result.Xslt));
        var xml = ThemeFixtures.Samples.Get(DocumentType.Invoice);
        var baseHtml = ThemeFixtures.Renderer.Render(source, xml).Html;
        var markedHtml = ThemeFixtures.Renderer.Render(result.Xslt, xml).Html;
        Assert.Equal(baseHtml, ThemeSlotMarkers.StripFromHtml(markedHtml));
        Assert.Equal(1, ThemeSlotMarkers.CountOccurrences(markedHtml, "existing-logo"));

        Assert.Equal(source, ThemeSlotMarkers.Clean(result.Xslt));
        Assert.Equal(["existing-logo", "existing-signature"],
            result.Catalog.Images!.Select(i => i.SlotId));
    }

    [Fact]
    public void Design_images_are_left_alone()
    {
        var source = ThemeFixtures.CompanyImagesTheme;
        var result = Analyzer.Analyze(source, DocumentType.Invoice, [], [Design, Design]);

        Assert.DoesNotContain("data-xc-placeholder", result.Xslt);
        Assert.DoesNotContain(result.Catalog.Slots, s => s.Id.StartsWith("existing-", StringComparison.Ordinal));
        Assert.Equal("docinfo-top", result.Catalog.Slots.Single(s => s.DefaultFor == ThemeAssetKind.Logo).Id);
        Assert.All(result.Catalog.Images!, i => Assert.Null(i.SlotId));
    }

    [Fact]
    public void Analysis_with_images_is_idempotent()
    {
        var once = Analyzer.Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [], LogoAndStamp);
        var twice = Analyzer.Analyze(once.Xslt, DocumentType.Invoice, [], LogoAndStamp);

        Assert.Equal(once.Xslt, twice.Xslt);
        Assert.Equal(once.Catalog.Slots, twice.Catalog.Slots);
    }

    [Fact]
    public void Same_role_images_share_one_slot()
    {
        // A5 temalarında sayfa iki kopya: logo iki kez geçer, kullanıcının logosu ikisinin de yerini almalı.
        var xslt = Synthetic("""
            <table><tr><td>Satıcı</td><td><img src="data:image/png;base64,AAAA" alt="a"/></td></tr></table>
            <table><tr><td>Satıcı</td><td><img src="data:image/png;base64,AAAA" alt="b"/></td></tr></table>
            """);
        var result = Analyzer.Analyze(xslt, DocumentType.Invoice, [], [new(ThemeImageRole.Logo, false), new(ThemeImageRole.Logo, false)]);

        Assert.Equal(2, result.Xslt.Split(@"data-xc-slot=""existing-logo""").Length - 1);
        Assert.Single(result.Catalog.Slots, s => s.Id == "existing-logo");
    }

    [Fact]
    public void Images_in_repeating_loops_and_dynamic_sources_are_skipped()
    {
        var xslt = Synthetic("""
            <xsl:for-each select="//cbc:Note"><img src="data:image/png;base64,AAAA"/></xsl:for-each>
            <img id="code128"/>
            <img src="{$logo}"/>
            """);

        Assert.Empty(Analyzer.DetectImages(xslt, DocumentType.Invoice));
        Assert.Contains(Analyzer.Analyze(xslt, DocumentType.Invoice, []).Catalog.Warnings, w => w.Code == "image-not-slottable");
    }

    [Fact]
    public void Upload_asks_for_image_roles_then_applies_them()
    {
        var xslt = ThemeFixtures.CompanyImagesTheme;

        var required = Assert.IsType<ThemeUploadOutcome.DecisionRequired>(Processor.Process(xslt, DocumentType.Invoice, null));
        Assert.Null(required.Inspection);
        Assert.Equal(2, required.Images.Count);

        // Sayısı tutmayan karar yeniden sorulur.
        Assert.IsType<ThemeUploadOutcome.DecisionRequired>(Processor.Process(xslt, DocumentType.Invoice, null, [new(ThemeImageRole.Logo, true)]));

        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(Processor.Process(xslt, DocumentType.Invoice, null, LogoAndStamp));
        Assert.Contains(ready.Catalog.Slots, s => s.Id == "existing-logo" && s.DefaultFor == ThemeAssetKind.Logo);
    }

    [Fact]
    public void Composer_targets_existing_logo_slot_by_default()
    {
        var marked = Analyzer.Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [], LogoAndStamp);
        var defaults = new Dictionary<ThemeAssetKind, string> { [ThemeAssetKind.Logo] = "existing-logo" };
        const string png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        var result = new ThemeAssetComposer(new ThemeImageValidator()).Compose(marked.Xslt, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, null, png, null, null, ThemeAlignment.Left)], [], defaults));

        Assert.True(result.Success, result.Error);
        Assert.Equal("existing-logo", result.AppliedSlots[ThemeAssetKind.Logo]);
        Assert.Contains("data-xc-placeholder", result.Xslt); // betik temadaki görseli hedefler
    }

    [Fact]
    public void Composer_emits_hide_request_without_user_image()
    {
        var marked = Analyzer.Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [], LogoAndStamp);
        var composer = new ThemeAssetComposer(new ThemeImageValidator());

        var result = composer.Compose(marked.Xslt, new ThemeComposeInput([], [], HideThemeImages: [ThemeAssetKind.Logo]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(@"""items"":[],""hide"":[""logo""]", result.Xslt);
        Assert.Contains(@"data-xc-overrides=""2""", result.Xslt);
    }

    [Fact]
    public void Hide_is_ignored_when_kind_has_user_image_or_theme_has_no_image_slot()
    {
        var composer = new ThemeAssetComposer(new ThemeImageValidator());
        var plain = Analyzer.Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [], [Design, Design]);

        var none = composer.Compose(plain.Xslt, new ThemeComposeInput([], [], HideThemeImages: [ThemeAssetKind.Logo]));
        Assert.DoesNotContain("data-xc-overrides", none.Xslt);

        var marked = Analyzer.Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [], LogoAndStamp);
        const string png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var withImage = composer.Compose(marked.Xslt, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, null, png, null, null, ThemeAlignment.Left)], [], HideThemeImages: [ThemeAssetKind.Logo]));
        Assert.Contains(@"""hide"":[]", withImage.Xslt);
    }

    [Fact]
    public void Admin_strip_of_logo_blanks_image_but_keeps_its_slot()
    {
        var source = ThemeFixtures.CompanyImagesTheme;
        var result = Analyzer.Analyze(source, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, false, Strip: true), new(ThemeImageRole.Signature, true)]);

        Assert.NotEqual(ThemeSlotStatus.Failed, result.Catalog.Status);
        Assert.Contains($@"data-xc-slot=""existing-logo"" data-xc-placeholder=""logo"" style=""float:right; width: 175px; height: 75px"" src=""{ThemeImageStripper.BlankImage}""", result.Xslt);
        Assert.Equal(PngCount(source) - 1, PngCount(result.Xslt));

        var logo = result.Catalog.Slots.Single(s => s.Id == "existing-logo");
        Assert.Equal(ThemeAssetKind.Logo, logo.DefaultFor);
        Assert.False(logo.Removable); // zaten boş
        var records = result.Catalog.Images!;
        Assert.True(records[0].Stripped);
        Assert.False(records[1].Stripped);
        Assert.True(records[1].Removable);
    }

    [Fact]
    public void Admin_strip_of_design_image_removes_it_entirely()
    {
        var source = ThemeFixtures.CompanyImagesTheme;
        var result = Analyzer.Analyze(source, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Design, false, Strip: true)]);

        Assert.NotEqual(ThemeSlotStatus.Failed, result.Catalog.Status);
        Assert.Equal(PngCount(source) - 1, PngCount(result.Xslt));
        Assert.DoesNotContain(result.Catalog.Slots, s => s.Id == "existing-signature");
        Assert.Contains(result.Catalog.Slots, s => s.Id == "customer-side"); // kaşe hücresi boşaldı → alıcının yanı
        Assert.Equal([(0, false), (1, true)], result.Catalog.Images!.Select(i => (i.Index, i.Stripped)));
        Assert.Equal(ThemeImageRole.Design, result.Catalog.Images![1].Role);
    }

    [Fact]
    public void Upload_with_strip_decisions_is_ready()
    {
        var xslt = ThemeFixtures.CompanyImagesTheme;
        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(Processor.Process(xslt, DocumentType.Invoice, null,
            [new(ThemeImageRole.Logo, false, Strip: true), new(ThemeImageRole.Design, false, Strip: true)]));

        Assert.Equal(PngCount(xslt) - 2, PngCount(ready.Xslt));
        Assert.Contains(ready.Catalog.Slots, s => s.Id == "existing-logo");
    }

    // ── Eski id'yi taşıyan görsel: boş görünse de içerik alamaz (void eleman), tutucu sayılmaz ──

    private const string ImageWithLegacyId =
        """<table><tr><td>Satıcı</td><td><img id="company_logo" src="data:image/png;base64,AAAA"/></td></tr></table>""";

    [Fact]
    public void Image_with_legacy_id_marked_as_logo_gets_a_single_slot_marker()
    {
        // Tutucu sayılsaydı R10 ve R9 aynı img'e iki kez data-xc-slot yazar, işaretli tema derlenemezdi.
        var result = Analyzer.Analyze(Synthetic(ImageWithLegacyId), DocumentType.Invoice, [], [new(ThemeImageRole.Logo, false)]);

        Assert.NotEqual(ThemeSlotStatus.Failed, result.Catalog.Status);
        Assert.Equal(ThemeAssetKind.Logo, result.Catalog.Slots.Single(s => s.Id == "existing-logo").DefaultFor);
        Assert.DoesNotContain(result.Catalog.Slots, s => s.LegacyId == "company_logo");
        Assert.Contains(@"<img data-xc-slot=""existing-logo"" data-xc-placeholder=""logo"" id=""company_logo""", result.Xslt);
        Assert.Single(Regex.Matches(result.Xslt, "data-xc-slot="));
    }

    [Fact]
    public void Image_with_legacy_id_kept_as_design_is_not_adopted()
    {
        // Yuva olsaydı runtime görselin içine div ekler, kullanıcının logosu görünmezdi.
        var result = Analyzer.Analyze(Synthetic(ImageWithLegacyId), DocumentType.Invoice, [], [Design]);

        Assert.DoesNotContain("data-xc-slot", result.Xslt);
        Assert.DoesNotContain(result.Catalog.Slots, s => s.LegacyId == "company_logo");
        Assert.DoesNotContain(result.Catalog.Warnings, w => w.Code == "holder-has-content");
    }

    [Fact]
    public void Second_slot_marker_on_same_element_is_refused()
    {
        // Güvenlik ağı: yinelenen nitelik temayı derlenemez yapar → yazım reddedilir, analiz "write-failed" raporlar.
        var doc = Document(Synthetic(ImageWithLegacyId));
        var img = doc.Body.Descendants().Single(e => ThemeDocument.IsLiteral(e, "img"));
        var placeholder = new ReducedSlot(new SlotCandidate("existing-logo", "R10", 1, SlotLayout.Block, null, null,
            ThemeAssetKind.Logo, 0, [img], ThemeAssetKind.Logo), "existing-logo", ThemeAssetKind.Logo);
        var adopted = new ReducedSlot(new SlotCandidate("legacy-logo", "R9", 90, SlotLayout.Block, null, img,
            ThemeAssetKind.Logo, 4), "legacy-logo", null);

        Assert.Contains(@"<img data-xc-slot=""existing-logo"" data-xc-placeholder=""logo""", ThemeSlotMarkers.Write(doc, [placeholder]));
        Assert.Throws<InvalidOperationException>(() => ThemeSlotMarkers.Write(doc, [placeholder, adopted]));
        Assert.Throws<InvalidOperationException>(() => ThemeSlotMarkers.Write(doc, [adopted, adopted with { Id = "header-right" }]));

        // Temizlenmemiş (biçimi tanınmayan) bir işaret zaten varsa da ikinci yazım sayılır.
        var stale = Document(Synthetic(ImageWithLegacyId.Replace("<img ", @"<img data-xc-slot=""X"" ")));
        var staleImg = stale.Body.Descendants().Single(e => ThemeDocument.IsLiteral(e, "img"));
        Assert.Throws<InvalidOperationException>(() => ThemeSlotMarkers.Write(stale, [new ReducedSlot(
            placeholder.Candidate with { Placeholders = [staleImg] }, "existing-logo", ThemeAssetKind.Logo)]));
    }

    // ── Başarısız analizde metni değiştiren görsel kararı ─────────────────────

    private const string HeaderImage =
        """<table><tr><td>Satıcı</td><td><img src="data:image/png;base64,AAAA" alt="logo"/></td></tr></table>""";

    [Fact]
    public void Unrenderable_theme_with_images_is_rejected_before_image_decision()
    {
        // Analiz render edemez (Failed); yükleme ise görsel kararı sorulmadan reddedilir — logosu silinmiş ya da
        // konumsuz bir tema saklanmaz.
        var xslt = Unrenderable(HeaderImage);
        var analysis = Analyzer.Analyze(xslt, DocumentType.Invoice, [], [new(ThemeImageRole.Logo, false)]);
        Assert.Equal(ThemeSlotStatus.Failed, analysis.Catalog.Status);
        Assert.Contains(analysis.Catalog.Warnings, w => w.Code == "render-failed");

        Assert.IsType<ThemeUploadOutcome.Rejected>(Processor.Process(xslt, DocumentType.Invoice, null));
        Assert.IsType<ThemeUploadOutcome.Rejected>(
            Processor.Process(xslt, DocumentType.Invoice, null, [new(ThemeImageRole.Logo, false, Strip: true)]));
    }

    // Derlenir ama örnek belgeyle çalışırken durur → analiz "render-failed".
    private static string Unrenderable(string body) => Synthetic(body + """<xsl:message terminate="yes">dur</xsl:message>""");

    private static ThemeDocument Document(string xslt)
    {
        Assert.True(XsltSourceMap.TryBuild(xslt, out var map, out var error), error);
        return ThemeDocument.TryCreate(map, DocumentType.Invoice)!;
    }

    private static int PngCount(string xslt) => xslt.Split("data:image/png;base64,").Length - 1;

    private static string Synthetic(string body) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <xsl:output method="html" encoding="UTF-8"/>
          <xsl:param name="logo" select="'x'"/>
          <xsl:template match="/">
            <html><body>
            {{body}}
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;
}
