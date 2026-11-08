using System.Diagnostics;

using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.Tests.ThemeSlots;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.EasyEdit;

/// <summary>Kolay düzenle kararlarının hata türüne göre eşlenmesi, görsel sınırları ve büyük girdide süre.</summary>
public class EasyEditVerdictTests
{
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly ThemeImageDecision[] LogoAndStamp = [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)];

    private static Task<EasyEditAnalysis> Analyze(string xslt, IReadOnlyList<ThemeImageRole>? roles = null,
        IXsltExecutionService? execution = null) =>
        new UploadedXsltAnalyzer(new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(new ThemeImageValidator()),
                new ThemeSlotAnalyzer(execution ?? ThemeFixtures.Execution, ThemeFixtures.Samples))
            .AnalyzeAsync(xslt, roles, Guid.NewGuid(), CancellationToken.None);

    private static async Task<string> MarkedCompanyTheme() =>
        (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            LogoAndStamp, Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;

    [Fact]
    public async Task Runtime_error_on_sample_is_invalid()
    {
        var xslt = ThemeFixtures.CompanyImagesTheme.Replace("</body>", """<xsl:message terminate="yes">dur</xsl:message></body>""");
        var analysis = await Analyze(xslt);
        Assert.Equal((EasyEditVerdict.Invalid, "render-failed"), (analysis.Verdict, analysis.ReasonCode));
    }

    [Fact]
    public async Task Runtime_error_line_comes_from_diagnostic()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: new FailingExecution(XsltFailureKind.Runtime, line: 7));
        Assert.Equal((EasyEditVerdict.Invalid, 7), (analysis.Verdict, analysis.ErrorLine));
    }

    [Fact]
    public async Task More_than_max_images_are_capped()
    {
        var extra = string.Concat(Enumerable.Repeat($"""<p><img src="{Png}" alt="company_logo" width="50" height="50"/></p>""", 19));
        var xslt = ThemeFixtures.CompanyImagesTheme.Replace("""<body style="width:800px;">""", $"""<body style="width:800px;">{extra}""");
        Assert.True(ThemeFixtures.Analyzer().DetectImages(xslt, DocumentType.Invoice).Count >= 21);

        var analysis = await Analyze(xslt);

        Assert.Equal(UploadedXsltAnalyzer.MaxImages, analysis.Images.Count);
    }

    [Fact]
    public async Task Client_roles_count_mismatch_falls_back_to_marker_with_warning()
    {
        var analysis = await Analyze(await MarkedCompanyTheme(), [ThemeImageRole.Design]);
        Assert.All(analysis.Images, i => Assert.Equal(EasyEditRoleSource.Marker, i.Source));
        Assert.Contains(analysis.Catalog!.Warnings, w => w.Code == "image-roles-mismatch");
    }

    [Fact]
    public async Task Timeout_is_unsuitable_too_heavy()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: new FailingExecution(XsltFailureKind.Timeout));
        Assert.Equal((EasyEditVerdict.Unsuitable, "too-heavy"), (analysis.Verdict, analysis.ReasonCode));
    }

    [Fact]
    public async Task User_busy_maps_to_busy_verdict()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: new FailingExecution(XsltFailureKind.UserBusy));
        Assert.Equal(EasyEditVerdict.Busy, analysis.Verdict);
    }

    [Fact]
    public async Task Oversized_render_output_is_unsuitable_too_heavy_quickly()
    {
        var html = new string('<', ThemeRenderVerifier.MaxComparedChars + 1);
        var sw = Stopwatch.StartNew();

        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: new FixedHtmlExecution(html));

        Assert.Equal((EasyEditVerdict.Unsuitable, "too-heavy"), (analysis.Verdict, analysis.ReasonCode));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
    }

    [Fact]
    public void Normalize_runs_in_linear_time_on_many_open_brackets()
    {
        var html = "a " + new string('<', 1_000_000) + " b";
        var sw = Stopwatch.StartNew();

        var normalized = HtmlComparison.Normalize(html);

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString());
        Assert.Equal(html, normalized);
    }

    [Theory]
    [InlineData("  <p> a   b </p>\n <br/>  c  ", "<p>a b</p><br/>c")]
    [InlineData("x <b>y</b> z < w", "x<b>y</b>z < w")]
    [InlineData("<a\n href='1'>t</a>  \t", "<a href='1'>t</a>")]
    public void Normalize_trims_space_around_tags_and_collapses_rest(string html, string expected) =>
        Assert.Equal(expected, HtmlComparison.Normalize(html));

    [Fact]
    public void Parse_unclosed_scripts_returns_quickly_unrecognized()
    {
        var inner = string.Concat(Enumerable.Repeat("<script>", 200_000));
        var sw = Stopwatch.StartNew();

        var parsed = OverridesBlockParser.Parse(inner);

        Assert.False(parsed.Recognized);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString());
    }

    private const string Item = "\"k\":\"logo\",\"slot\":\"existing-logo\",\"src\":\"x\",\"a\":\"left\"";

    private static string V2Block(string item, string root = "") =>
        $$"""<script type="application/json" data-xc-overrides-data="2">{"v":2,"items":[{{{item}}}]{{root}}}</script>""";

    [Fact]
    public void Parse_valid_v2_block_is_recognized() => Assert.True(OverridesBlockParser.Parse(V2Block(Item + ",\"w\":120")).Recognized);

    [Theory]
    [InlineData(Item + ",\"xs\":[\"bad id\"]", "")]
    [InlineData(Item, ",\"hide\":[\"x\"]")]
    [InlineData(Item + ",\"keep\":\"1\"", "")]
    [InlineData("\"k\":\"logo\",\"slot\":\"Bad Slot!\",\"src\":\"x\",\"a\":\"left\"", "")]
    [InlineData(Item + ",\"w\":5000", "")]
    public void Parse_invalid_v2_fields_are_unrecognized(string item, string root) =>
        Assert.False(OverridesBlockParser.Parse(V2Block(item, root)).Recognized);

    [Fact]
    public async Task Inspect_valid_extra_slots_is_not_keepable()
    {
        var composed = new ThemeAssetComposer(new ThemeImageValidator()).Compose(await MarkedCompanyTheme(), new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Signature, null, Png, null, null, ThemeAlignment.Center)], []));
        Assert.True(composed.Success, composed.Error);
        var withExtra = composed.Xslt.Replace("{\"k\":\"signature\",", "{\"k\":\"signature\",\"xs\":[\"qr-below\"],");
        Assert.NotEqual(composed.Xslt, withExtra);

        var inspection = new ThemeOverridesProcessor(new ThemeImageValidator()).Inspect(withExtra);

        Assert.True(inspection.Recognized);
        Assert.False(inspection.Keepable);
    }

    private sealed class FailingExecution(XsltFailureKind kind, int line = 0) : IXsltExecutionService
    {
        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new XsltRunResult(null, XsltEngineKind.DotNet, null, false,
                new XsltDiagnostic("Yürütme başarısız.", line, 0, kind, XsltEngineKind.DotNet)));
    }

    private sealed class FixedHtmlExecution(string html) : IXsltExecutionService
    {
        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new XsltRunResult(html, XsltEngineKind.DotNet, null, false, null));
    }
}
