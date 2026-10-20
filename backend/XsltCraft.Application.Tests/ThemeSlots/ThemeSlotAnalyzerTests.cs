using System.Text.RegularExpressions;

using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class ThemeSlotAnalyzerTests
{
    private static readonly IThemeSlotAnalyzer Analyzer = ThemeFixtures.Analyzer();

    public static TheoryData<string, DocumentType> Renderable => new()
    {
        { ThemeFixtures.TestTheme, DocumentType.Invoice },
        { ThemeFixtures.GibEInvoice, DocumentType.Invoice },
        { ThemeFixtures.GibTemplate, DocumentType.Invoice },
        { ThemeFixtures.GibEIrsaliye, DocumentType.Despatch },
    };

    [Theory]
    [InlineData(ThemeFixtures.TestTheme, DocumentType.Invoice,
        "seller-top,gib-logo-below,qr-below,customer-side,docinfo-top,totals-left,notes-bottom,notes-side", "docinfo-top", "customer-side")]
    [InlineData(ThemeFixtures.GibEInvoice, DocumentType.Invoice,
        "seller-top,gib-logo-below,qr-below,customer-side,docinfo-top,totals-left,notes-bottom,notes-side", "qr-below", "customer-side")]
    [InlineData(ThemeFixtures.GibTemplate, DocumentType.Invoice,
        "seller-top,gib-logo-below,qr-below,customer-side,docinfo-top,totals-left,notes-bottom,header-right", "header-right", "customer-side")]
    [InlineData(ThemeFixtures.GibEIrsaliye, DocumentType.Despatch,
        "seller-top,gib-logo-below,qr-below,customer-side,docinfo-top,notes-bottom", "docinfo-top", "customer-side")]
    public void Finds_expected_slots_and_keeps_legacy_defaults(string fixture, DocumentType type, string slots, string logo, string signature)
    {
        var result = Analyzer.Analyze(ThemeFixtures.Load(fixture), type, []);

        Assert.Equal(ThemeSlotStatus.Ok, result.Catalog.Status);
        Assert.Equal(slots.Split(','), result.Catalog.Slots.Select(s => s.Id));
        Assert.Equal(logo, result.Catalog.Slots.Single(s => s.DefaultFor == ThemeAssetKind.Logo).Id);
        Assert.Equal(signature, result.Catalog.Slots.Single(s => s.DefaultFor == ThemeAssetKind.Signature).Id);
    }

    [Fact]
    public void Existing_holders_are_adopted_not_duplicated()
    {
        var result = Analyzer.Analyze(ThemeFixtures.Load(ThemeFixtures.TestTheme), DocumentType.Invoice, []);

        var adopted = result.Catalog.Slots.Where(s => !s.Inserted).ToDictionary(s => s.Id, s => s.LegacyId);
        Assert.Equal("imza", adopted["customer-side"]);
        Assert.Equal("company_logo", adopted["docinfo-top"]);
        Assert.Contains(@"<div data-xc-slot=""customer-side"" id=""imza"">", result.Xslt);
        Assert.Contains(@"<div data-xc-slot=""docinfo-top"" id=""company_logo"">", result.Xslt);
    }

    [Theory]
    [MemberData(nameof(Renderable))]
    public void Marked_theme_is_safe_compiles_and_renders_identically_without_markers(string fixture, DocumentType type)
    {
        var source = ThemeFixtures.Load(fixture);
        var result = Analyzer.Analyze(source, type, []);

        Assert.Null(XsltSafety.FindThreat(result.Xslt));
        SecureXslt.Compile(result.Xslt);

        var xml = ThemeFixtures.Samples.Get(type);
        var baseHtml = ThemeFixtures.Renderer.Render(source, xml).Html;
        var markedHtml = ThemeFixtures.Renderer.Render(result.Xslt, xml).Html;
        Assert.Equal(baseHtml, ThemeSlotMarkers.StripFromHtml(markedHtml));

        foreach (var slot in result.Catalog.Slots.Where(s => !s.Conditional))
            Assert.Equal(1, ThemeSlotMarkers.CountOccurrences(markedHtml, slot.Id));
    }

    [Theory]
    [MemberData(nameof(Renderable))]
    public void Root_tag_qr_and_ettn_are_byte_identical(string fixture, DocumentType type)
    {
        var source = ThemeFixtures.Load(fixture);
        var result = Analyzer.Analyze(source, type, []);

        Assert.Equal(RootTag(source), RootTag(result.Xslt));
        Assert.Equal(Element(source, "qrcode"), Element(result.Xslt, "qrcode"));
        Assert.Equal(Element(source, "qrvalue"), Element(result.Xslt, "qrvalue"));
        Assert.Equal(Regex.Matches(source, "cbc:UUID").Count, Regex.Matches(result.Xslt, "cbc:UUID").Count);
    }

    [Theory]
    [MemberData(nameof(Renderable))]
    public void Analysis_is_idempotent(string fixture, DocumentType type)
    {
        var once = Analyzer.Analyze(ThemeFixtures.Load(fixture), type, []);
        var twice = Analyzer.Analyze(once.Xslt, type, []);

        Assert.Equal(once.Xslt, twice.Xslt);
        Assert.Equal(once.Catalog.Slots, twice.Catalog.Slots);
    }

    [Fact]
    public void Theme_that_cannot_render_fails_and_stays_unmarked()
    {
        // general.xslt XSLT 2.0 fonksiyonu (exists()) kullanıyor; önizleme motoru da render edemiyor.
        var source = ThemeFixtures.Load(ThemeFixtures.GibGeneral);
        var result = Analyzer.Analyze(source, DocumentType.Invoice, []);

        Assert.Equal(ThemeSlotStatus.Failed, result.Catalog.Status);
        Assert.Empty(result.Catalog.Slots);
        Assert.Equal(source, result.Xslt);
        Assert.Contains(result.Catalog.Warnings, w => w.Code == "render-failed");
    }

    [Fact]
    public void Xml_output_method_gets_comment_child_so_html_parser_does_not_swallow_content()
    {
        var xslt = Synthetic(method: "xml");
        var result = Analyzer.Analyze(xslt, DocumentType.Invoice, []);

        Assert.NotEmpty(result.Catalog.Slots);
        Assert.Contains(@"data-xc-inserted=""1""><xsl:comment/></div>", result.Xslt);
    }

    [Fact]
    public void Html_output_method_gets_plain_empty_marker()
    {
        var result = Analyzer.Analyze(Synthetic(method: "html"), DocumentType.Invoice, []);

        Assert.Contains(result.Catalog.Slots, s => s.Id == "qr-below");
        Assert.Contains(@"<div data-xc-slot=""qr-below"" data-xc-layout=""block"" data-xc-inserted=""1""></div>", result.Xslt);
    }

    [Fact]
    public void Holder_with_content_is_not_offered()
    {
        var xslt = Synthetic(method: "html", holder: @"<div id=""company_logo""><img src=""data:image/png;base64,AAAA""/></div>");
        var result = Analyzer.Analyze(xslt, DocumentType.Invoice, []);

        Assert.DoesNotContain(result.Catalog.Slots, s => s.LegacyId == "company_logo");
        Assert.Contains(result.Catalog.Warnings, w => w.Code == "holder-has-content");
    }

    [Fact]
    public void Template_without_body_fails()
    {
        const string xslt = """
            <?xml version="1.0" encoding="UTF-8"?>
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/"><p>hi</p></xsl:template>
            </xsl:stylesheet>
            """;
        var result = Analyzer.Analyze(xslt, DocumentType.Invoice, []);

        Assert.Equal(ThemeSlotStatus.Failed, result.Catalog.Status);
        Assert.Contains(result.Catalog.Warnings, w => w.Code == "no-body-template");
    }

    // ── yardımcılar ───────────────────────────────────────────────────────────

    private static string RootTag(string xslt)
    {
        var start = xslt.IndexOf("<xsl:stylesheet", StringComparison.Ordinal);
        return xslt[start..(xslt.IndexOf('>', start) + 1)];
    }

    private static string Element(string xslt, string id)
    {
        var m = Regex.Match(xslt, $@"<(\w+)[^>]*\bid=""{id}""[^>]*?(/>|>.*?</\1>)", RegexOptions.Singleline);
        return m.Success ? m.Value : "";
    }

    internal static string Synthetic(string method, string holder = "") => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
            xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <xsl:output method="{{method}}" encoding="UTF-8"/>
          <xsl:variable name="XML" select="/"/>
          <xsl:template match="/">
            <html><body>
              <xsl:for-each select="$XML">
                <table><tbody><tr>
                  <td><xsl:value-of select="n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name"/></td>
                  <td><div id="qrcode"/>{{holder}}</td>
                </tr></tbody></table>
              </xsl:for-each>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;
}
