using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class ThemeUploadProcessorTests
{
    private static readonly IThemeUploadProcessor Processor =
        new ThemeUploadProcessor(new ThemeOverridesProcessor(new ThemeImageValidator()), ThemeFixtures.Analyzer(), ThemeFixtures.Renderer, ThemeFixtures.Samples);

    [Fact]
    public void Theme_with_embedded_block_requires_decision()
    {
        var outcome = Processor.Process(ThemeFixtures.Load(ThemeFixtures.TestTheme), DocumentType.Invoice, decision: null);

        var required = Assert.IsType<ThemeUploadOutcome.DecisionRequired>(outcome);
        Assert.Equal(2, required.Inspection!.Images.Count);
    }

    [Fact]
    public void Strip_then_analysis_offers_all_slots()
    {
        var outcome = Processor.Process(ThemeFixtures.Load(ThemeFixtures.TestTheme), DocumentType.Invoice, OverridesDecision.Strip);

        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(outcome);
        Assert.Equal(ThemeSlotStatus.Ok, ready.Catalog.Status);
        Assert.Equal(8, ready.Catalog.Slots.Count);
        Assert.Equal("strip", ready.Catalog.Overrides!.Decision);
        Assert.DoesNotContain(OverridesBlockText.Start, ready.Xslt);

        var roundTrip = ThemeSlotCatalogJson.TryDeserialize(ready.CatalogJson);
        Assert.Equal(ready.Catalog.Slots, roundTrip!.Slots);
    }

    [Fact]
    public void Keep_marks_holders_occupied()
    {
        var outcome = Processor.Process(ThemeFixtures.Load(ThemeFixtures.TestTheme), DocumentType.Invoice, OverridesDecision.Keep);

        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(outcome);
        Assert.Equal("keep", ready.Catalog.Overrides!.Decision);
        Assert.Equal(2, ready.Catalog.Overrides.ImagesKept);
        Assert.Equal(["company_logo", "imza"], ready.Catalog.OccupiedHolders.Order());
    }

    [Fact]
    public void Keep_of_unkeepable_block_is_rejected()
    {
        var xslt = ThemeFixtures.Load(ThemeFixtures.TestTheme).Replace("data:image/png;base64,", "data:image/svg+xml;base64,");
        var outcome = Processor.Process(xslt, DocumentType.Invoice, OverridesDecision.Keep);

        Assert.IsType<ThemeUploadOutcome.Rejected>(outcome);
    }

    [Fact]
    public void Non_utf8_theme_is_stored_untouched()
    {
        var xslt = ThemeFixtures.Load(ThemeFixtures.GibEIrsaliye).Replace("encoding=\"UTF-8\"?>", "encoding=\"windows-1254\"?>");
        var outcome = Processor.Process(xslt, DocumentType.Despatch, decision: null);

        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(outcome);
        Assert.True(ready.Original);
        Assert.Equal(ThemeSlotStatus.Skipped, ready.Catalog.Status);
    }

    [Fact]
    public void Unrenderable_theme_is_rejected_before_any_decision()
    {
        // Bozuk tema için blok kararı sormak anlamsız: 409'dan önce reddedilir, karar ne olursa olsun.
        var xslt = Unrenderable(ThemeFixtures.Load(ThemeFixtures.TestTheme));

        foreach (var decision in new OverridesDecision?[] { null, OverridesDecision.Keep, OverridesDecision.Strip })
        {
            var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(Processor.Process(xslt, DocumentType.Invoice, decision));
            Assert.Contains("Tema örnek fatura ile işlenemedi, yüklenmedi", rejected.Message);
        }
    }

    [Fact]
    public void Theme_using_xslt2_functions_is_rejected_with_hint()
    {
        // general.xslt XSLT 2.0 fonksiyonu (exists()) kullanıyor: önizleme motoru render edemez → yüklenmez.
        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(
            Processor.Process(ThemeFixtures.Load(ThemeFixtures.GibGeneral), DocumentType.Invoice, null));

        Assert.Contains("exists()", rejected.Message);
        Assert.Contains("XSLT 1.0", rejected.Message);
    }

    [Fact]
    public void Not_compiling_theme_is_rejected()
    {
        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(
            Processor.Process(NotCompiling(block: ""), DocumentType.Invoice, null));

        Assert.Contains("işlenemedi, yüklenmedi", rejected.Message);
    }

    [Fact]
    public void Despatch_theme_is_checked_with_sample_despatch()
    {
        var xslt = Unrenderable(ThemeFixtures.Load(ThemeFixtures.GibEIrsaliye));
        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(Processor.Process(xslt, DocumentType.Despatch, null));

        Assert.Contains("örnek irsaliye ile", rejected.Message);
    }

    [Fact]
    public void Projector_labels_slots_by_document_type()
    {
        var ready = (ThemeUploadOutcome.Ready)Processor.Process(ThemeFixtures.Load(ThemeFixtures.GibEIrsaliye), DocumentType.Despatch, null);
        var options = new ThemeSlotProjector().ForUser(ready.CatalogJson, DocumentType.Despatch);

        Assert.Contains(options, o => o.Id == "seller-top" && o.Label == "Gönderici bilgilerinin üstü");
        Assert.Contains(options, o => o.Id == "docinfo-top" && o.Label == "İrsaliye bilgilerinin üstü" && o.DefaultFor == "logo");
        Assert.Equal(options.OrderBy(o => o.Order), options);
    }

    // Derlenir ama örnek belgeyle çalışırken durur → analiz "render-failed".
    private static string Unrenderable(string xslt) =>
        xslt.Replace("</body>", """<xsl:message terminate="yes">dur</xsl:message></body>""");

    private static string NotCompiling(string block) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="html"/>
          <xsl:template match="/">
            <html><body><p><xsl:value-of select="$tanimsiz"/></p>{{block}}</body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;
}
