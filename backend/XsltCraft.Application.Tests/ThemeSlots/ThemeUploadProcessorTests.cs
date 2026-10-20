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
        new ThemeUploadProcessor(new ThemeOverridesProcessor(new ThemeImageValidator()), ThemeFixtures.Analyzer());

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
    public void Failed_analysis_rejects_keep_but_allows_block_strip()
    {
        // Koru, bloğu temaya gömüp tutucuları konum dışı bırakır — analiz başarısızken saklanmaz. Kaldır yalnız
        // bloğu siler (yuvadan bağımsız); son kapıdan geçerse saklanır.
        var xslt = Unrenderable(ThemeFixtures.Load(ThemeFixtures.TestTheme));

        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(Processor.Process(xslt, DocumentType.Invoice, OverridesDecision.Keep));
        Assert.Contains("Tema analiz edilemedi", rejected.Message);
        Assert.Contains("'Kaldır'", rejected.Message);

        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(Processor.Process(xslt, DocumentType.Invoice, OverridesDecision.Strip));
        Assert.Equal(ThemeSlotStatus.Failed, ready.Catalog.Status);
        Assert.Equal("strip", ready.Catalog.Overrides!.Decision);
        Assert.DoesNotContain(OverridesBlockText.Start, ready.Xslt);
        SecureXslt.Compile(ready.Xslt);
    }

    [Fact]
    public void Unrenderable_theme_without_decisions_is_stored_as_uploaded()
    {
        // general.xslt XSLT 2.0 fonksiyonu kullanıyor: önizleme motoru render edemez ama tema yüklenebilir.
        var xslt = ThemeFixtures.Load(ThemeFixtures.GibGeneral);
        var ready = Assert.IsType<ThemeUploadOutcome.Ready>(Processor.Process(xslt, DocumentType.Invoice, null));

        Assert.Equal(ThemeSlotStatus.Failed, ready.Catalog.Status);
        Assert.Equal(xslt, ready.Xslt);
    }

    [Fact]
    public void Server_transformed_text_must_compile_but_uploaded_text_is_taken_as_is()
    {
        // Konum adayı yok → analiz derlemez. Derlenmeyen tema olduğu gibi saklanır (bugünkü davranış); sunucu metni
        // değiştirdiyse (blok kaldırıldı) son kapı derler ve reddeder.
        var plain = NotCompiling(block: "");
        Assert.Equal(plain, Assert.IsType<ThemeUploadOutcome.Ready>(Processor.Process(plain, DocumentType.Invoice, null)).Xslt);

        var withBlock = NotCompiling(block: $"{OverridesBlockText.Start}<div>eski</div>{OverridesBlockText.End}");
        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(Processor.Process(withBlock, DocumentType.Invoice, OverridesDecision.Strip));
        Assert.Contains("derlenemedi", rejected.Message);
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
