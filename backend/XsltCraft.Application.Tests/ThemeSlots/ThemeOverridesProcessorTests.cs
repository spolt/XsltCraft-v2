using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class ThemeOverridesProcessorTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly IThemeOverridesProcessor Processor = new ThemeOverridesProcessor(new ThemeImageValidator());

    [Fact]
    public void Inspects_v1_block_of_test_theme()
    {
        var inspection = Processor.Inspect(ThemeFixtures.Load(ThemeFixtures.TestTheme));

        Assert.True(inspection.Present);
        Assert.True(inspection.Keepable, inspection.Reason);
        Assert.Equal(1, inspection.Version);
        Assert.Equal(2, inspection.Images.Count);
        Assert.Equal(["company_logo", "companyLogo"], inspection.Images[0].Targets);
        Assert.Equal(["imza", "companyKase"], inspection.Images[1].Targets);
        Assert.All(inspection.Images, i => Assert.NotNull(i.DataUrl));
    }

    [Fact]
    public void Keep_writes_images_statically_and_removes_script()
    {
        var applied = Processor.Apply(ThemeFixtures.Load(ThemeFixtures.TestTheme), OverridesDecision.Keep);

        Assert.True(applied.Success, applied.Error);
        Assert.DoesNotContain(OverridesBlockText.Start, applied.Xslt);
        Assert.DoesNotContain("function setImg(", applied.Xslt);
        Assert.Equal(["company_logo", "imza"], applied.OccupiedHolderIds.Order());
        Assert.Contains(@"<div data-xc-occupied=""1"" id=""company_logo""><div style=""display:flex;justify-content:flex-end;", applied.Xslt);

        var html = ThemeFixtures.Renderer.Render(applied.Xslt, ThemeFixtures.Samples.Get(DocumentType.Invoice)).Html;
        Assert.Contains($@"<img src=""{Png}""", html);
    }

    [Fact]
    public void Kept_holders_are_not_offered_as_slots()
    {
        var applied = Processor.Apply(ThemeFixtures.Load(ThemeFixtures.TestTheme), OverridesDecision.Keep);
        var analysis = ThemeFixtures.Analyzer().Analyze(applied.Xslt, DocumentType.Invoice, applied.OccupiedHolderIds);

        Assert.DoesNotContain(analysis.Catalog.Slots, s => s.LegacyId is "imza" or "company_logo");
        Assert.Equal(["company_logo", "imza"], analysis.Catalog.OccupiedHolders.Order());
        Assert.Contains(analysis.Catalog.Slots, s => s.Id == "qr-below");
    }

    [Fact]
    public void Strip_removes_block_only()
    {
        var source = ThemeFixtures.Load(ThemeFixtures.TestTheme);
        var applied = Processor.Apply(source, OverridesDecision.Strip);

        Assert.True(applied.Success);
        Assert.DoesNotContain(OverridesBlockText.Start, applied.Xslt);
        Assert.DoesNotContain(OverridesBlockText.End, applied.Xslt);
        Assert.Empty(applied.OccupiedHolderIds);
        SecureXslt.Compile(applied.Xslt);
    }

    [Fact]
    public void Svg_image_is_not_keepable()
    {
        var xslt = WithBlock("setImg(['company_logo'],'data:image/svg+xml;base64,PHN2Zy8+',null,null,'left');");
        var inspection = Processor.Inspect(xslt);

        Assert.True(inspection.Present);
        Assert.False(inspection.Keepable);
        Assert.Equal(1, inspection.Version);
    }

    [Fact]
    public void Unknown_script_is_not_keepable()
    {
        var xslt = WithBlock("", extra: "<script>alert(1)</script>");
        Assert.False(Processor.Inspect(xslt).Keepable);
    }

    [Fact]
    public void Two_blocks_are_not_keepable_but_strip_removes_both()
    {
        var single = WithBlock($"setImg(['company_logo'],'{Png}',null,null,'left');");
        var block = single[single.IndexOf(OverridesBlockText.Start, StringComparison.Ordinal)..(single.IndexOf(OverridesBlockText.End, StringComparison.Ordinal) + OverridesBlockText.End.Length)];
        var doubled = single.Replace("</body>", block + "</body>");

        Assert.False(Processor.Inspect(doubled).Keepable);
        var stripped = Processor.Apply(doubled, OverridesDecision.Strip);
        Assert.DoesNotContain(OverridesBlockText.Start, stripped.Xslt);
    }

    [Fact]
    public void Bank_table_is_kept_as_part_of_theme()
    {
        var bank = """<div style="margin:0"><table style="width:100%"><thead><tr><th style="x">Banka Adı</th><th style="x">IBAN</th></tr></thead><tbody><tr><td style="y">Örnek</td><td style="y">TR00</td></tr></tbody></table></div>""";
        var xslt = WithBlock($"setImg(['company_logo'],'{Png}',10,10,'center');", extra: bank);

        var applied = Processor.Apply(xslt, OverridesDecision.Keep);
        Assert.True(applied.Success, applied.Error);
        Assert.Equal(1, applied.BankRowsKept);
        Assert.Contains("<td style=\"y\">TR00</td>", applied.Xslt);
        Assert.DoesNotContain(OverridesBlockText.Start, applied.Xslt);
    }

    [Fact]
    public void Target_missing_in_theme_is_not_keepable()
    {
        var xslt = WithBlock($"setImg(['olmayanId'],'{Png}',null,null,'left');");
        var inspection = Processor.Inspect(xslt);

        Assert.False(inspection.Keepable);
        Assert.NotNull(inspection.Images.Single().Problem);
    }

    [Fact]
    public void Image_target_is_not_keepable()
    {
        // v1: hedef id temada bir <img> — boş görünse de içine içerik yazılamaz; blok yalnız kaldırılabilir.
        var xslt = WithBlock($"setImg(['company_logo'],'{Png}',null,null,'left');")
            .Replace(@"<div id=""company_logo""></div>", @"<img id=""company_logo"" src=""data:image/png;base64,AAAA""/>");
        var inspection = Processor.Inspect(xslt);

        Assert.True(inspection.Present);
        Assert.False(inspection.Keepable);
        Assert.NotNull(inspection.Images.Single().Problem);
        Assert.False(Processor.Apply(xslt, OverridesDecision.Keep).Success);
    }

    [Fact]
    public void Theme_image_slot_is_not_a_keep_target()
    {
        // v2: yuva temadaki görselin kendisi (data-xc-placeholder) ve yedek id yok → korunamaz.
        var marked = ThemeFixtures.Analyzer().Analyze(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)]).Xslt;
        var composed = new ThemeAssetComposer(new ThemeImageValidator()).Compose(marked, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Logo, "existing-logo", Png, null, null, ThemeAlignment.Left)], []));
        Assert.True(composed.Success, composed.Error);

        var inspection = Processor.Inspect(composed.Xslt);
        Assert.Equal(2, inspection.Version);
        Assert.False(inspection.Keepable);
        Assert.False(Processor.Apply(composed.Xslt, OverridesDecision.Keep).Success);
    }

    private static string WithBlock(string calls, string extra = "") => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="html"/>
          <xsl:template match="/">
            <html><body>
              <div id="company_logo"></div>
              <div id="imza"/>
        <!-- DEV-MODE-OVERRIDES:START -->
        <script>(function(){
          function setImg(ids,url,w,h,align){}
          window.addEventListener('load',function(){
            {{calls}}
          });
        })();</script>
        {{extra}}
        <!-- DEV-MODE-OVERRIDES:END -->
        </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;
}
