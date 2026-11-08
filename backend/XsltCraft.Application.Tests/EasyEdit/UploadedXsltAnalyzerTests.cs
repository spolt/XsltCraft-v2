using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.Tests.ThemeSlots;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.EasyEdit;

public class UploadedXsltAnalyzerTests
{
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    private const string Gif = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
    private const string Jpeg = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHSEeHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==";
    private const string Svg = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=";

    // Başlıktaki firma logosunun kaynağı (fixture'da tek geçiş).
    private static string CompanyLogoSrc(string src) => $@"src=""{src}"" alt=""company_logo""/>";

    private static readonly ThemeImageDecision[] LogoAndStamp = [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)];

    private static IUploadedXsltAnalyzer Analyzer(IXsltExecutionService? execution = null) => new UploadedXsltAnalyzer(
        new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(new ThemeImageValidator()),
        new ThemeSlotAnalyzer(execution ?? ThemeFixtures.Execution, ThemeFixtures.Samples));

    private static Task<EasyEditAnalysis> Analyze(string xslt, IReadOnlyList<ThemeImageRole>? roles = null,
        IXsltExecutionService? execution = null) =>
        Analyzer(execution).AnalyzeAsync(xslt, roles, Guid.NewGuid(), CancellationToken.None);

    private static async Task<string> MarkedCompanyTheme() =>
        (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            LogoAndStamp, Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;

    [Fact]
    public async Task AnalyzeAsync_PriorMarkers_RolesReadAsMarker_ClientRolesOverride()
    {
        var marked = await MarkedCompanyTheme();

        var fromMarker = await Analyze(marked);
        Assert.Equal([ThemeImageRole.Logo, ThemeImageRole.Signature], fromMarker.Images.Select(i => i.Role));
        Assert.All(fromMarker.Images, i => Assert.Equal(EasyEditRoleSource.Marker, i.Source));

        var fromClient = await Analyze(marked, [ThemeImageRole.Design, ThemeImageRole.Logo]);
        Assert.Equal([ThemeImageRole.Design, ThemeImageRole.Logo], fromClient.Images.Select(i => i.Role));
        Assert.All(fromClient.Images, i => Assert.Equal(EasyEditRoleSource.Client, i.Source));
    }

    [Fact]
    public async Task AnalyzeAsync_ComposedOutput_PreviousReturnsImagesBankAndHide()
    {
        var marked = await MarkedCompanyTheme();
        BankInfoInput[] banks = [new("Ziraat Bankası", "TR00 0001 0000 0000 0000 0000 01")];
        var composed = new ThemeAssetComposer(new ThemeImageValidator()).Compose(marked, new ThemeComposeInput(
            [new ThemeAssetInput(ThemeAssetKind.Signature, null, Png, null, null, ThemeAlignment.Center)], banks,
            HideThemeImages: [ThemeAssetKind.Logo]));
        Assert.True(composed.Success, composed.Error);

        var analysis = await Analyze(composed.Xslt);

        var previous = Assert.IsType<EasyEditPrevious>(analysis.Previous);
        Assert.True(previous.Recognized);
        var image = Assert.Single(previous.Images);
        Assert.Equal(ThemeAssetKind.Signature, image.Kind);
        Assert.Equal(Png, image.DataUrl);
        Assert.Equal(banks, previous.Bank);
        Assert.Equal([ThemeAssetKind.Logo], previous.Hide);
        // Önceki blok temizlenmiş metin döner.
        Assert.DoesNotContain(OverridesBlockText.Start, analysis.Xslt ?? "");
    }

    [Fact]
    public async Task AnalyzeAsync_ComposedFreeImage_PreviousReturnsFreeImages()
    {
        var marked = await MarkedCompanyTheme();
        var slot = ThemeSlotMarkers.SlotIdsIn(marked).Order(StringComparer.Ordinal).First(s => !s.StartsWith("existing-", StringComparison.Ordinal));
        var composed = new ThemeAssetComposer(new ThemeImageValidator()).Compose(marked, new ThemeComposeInput([], [],
            FreeImages: [new FreeImageInput(slot, Jpeg, 120, null, ThemeAlignment.Right)]));
        Assert.True(composed.Success, composed.Error);

        var analysis = await Analyze(composed.Xslt);

        var previous = Assert.IsType<EasyEditPrevious>(analysis.Previous);
        Assert.True(previous.Recognized, previous.Problem);
        Assert.Empty(previous.Images);
        Assert.Equal(new OverridesFreeImageInfo(slot, Jpeg, 120, null, "right", true, null), Assert.Single(previous.FreeImages));
        Assert.DoesNotContain(OverridesBlockText.Start, analysis.Xslt ?? "");

        var dto = EasyEditResponseMapper.ToResponse(analysis, new ThemeSlotProjector()).Previous!;
        Assert.Equal(new EasyEditPreviousFreeImageDto(slot, Jpeg, 120, null, "right", true, null), Assert.Single(dto.FreeImages));
    }

    [Fact]
    public async Task AnalyzeAsync_PngImages_MovableWithoutBlocker()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme);

        Assert.NotEmpty(analysis.Images);
        Assert.All(analysis.Images, i =>
        {
            Assert.True(i.Movable);
            Assert.Null(i.StaticBlocker);
        });
    }

    [Theory]
    [InlineData("jpeg-labelled-png")]
    [InlineData("large-png")]
    [InlineData("gif")]
    public async Task AnalyzeAsync_RasterImagesFailingUploadRules_StayMovable(string variant)
    {
        var src = variant switch
        {
            "jpeg-labelled-png" => "data:image/png;base64," + Jpeg[(Jpeg.IndexOf(',') + 1)..],
            "large-png" => "data:image/png;base64," + new string('A', 820_000),
            _ => Gif,
        };
        Assert.False(new ThemeImageValidator().Check(src).Valid); // yükleme kuralı reddeder; taşınabilirlik ondan bağımsız

        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme.Replace(Png, src));

        Assert.NotEmpty(analysis.Images);
        Assert.All(analysis.Images, i =>
        {
            Assert.True(i.Movable);
            Assert.Null(i.StaticBlocker);
        });
    }

    [Fact]
    public async Task AnalyzeAsync_SvgImage_NoDataUrlBlocker()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme.Replace(CompanyLogoSrc(Png), CompanyLogoSrc(Svg)));

        var svg = Assert.Single(analysis.Images, i => i.PreviewDataUrl is null);
        Assert.False(svg.Movable);
        Assert.Equal(StaticImageSources.NoDataUrl, svg.StaticBlocker);
        Assert.Contains(analysis.Images, i => i.Movable);
    }

    [Fact]
    public async Task AnalyzeAsync_AttributeValueTemplate_DynamicAttributeBlocker()
    {
        var theme = ThemeFixtures.CompanyImagesTheme.Replace(CompanyLogoSrc(Png),
            $@"title=""{{n1:Invoice/cbc:ID}}"" {CompanyLogoSrc(Png)}");
        Assert.NotEqual(ThemeFixtures.CompanyImagesTheme, theme);

        var analysis = await Analyze(theme);

        var dynamic = Assert.Single(analysis.Images, i => !i.Movable);
        Assert.Equal(StaticImageSources.DynamicAttribute, dynamic.StaticBlocker);
        Assert.NotNull(dynamic.PreviewDataUrl);
    }

    [Fact]
    public void ResponseMapper_NormalizesPreviewDataUrlPrefixOnly()
    {
        var payload = Png[(Png.IndexOf(',') + 1)..];
        Assert.Equal(Png, StaticImageSources.Normalize("DATA:IMAGE/PNG;BASE64," + payload));
        Assert.Equal("data:image/jpeg;base64,AbC=", StaticImageSources.Normalize("data:image/JPG;base64,AbC="));
        Assert.Equal("data:image/svg+xml;base64,AbC=", StaticImageSources.Normalize("data:image/svg+xml;base64,AbC="));
    }

    [Fact]
    public async Task AnalyzeAsync_OkCatalog_Suitable_RunsOnRawRoute()
    {
        var execution = new RouteRecordingExecution(ThemeFixtures.Execution);

        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: execution);

        Assert.Equal(EasyEditVerdict.Suitable, analysis.Verdict);
        Assert.NotNull(analysis.Xslt);
        Assert.NotEmpty(execution.Requests);
        Assert.All(execution.Requests, r => Assert.Equal(XsltRoute.Raw, r.Route));
    }

    [Fact]
    public async Task AnalyzeAsync_UnparseableXslt_Invalid()
    {
        var execution = new RouteRecordingExecution(ThemeFixtures.Execution);
        var broken = ThemeFixtures.CompanyImagesTheme.Replace("</body>", "</bdy>");

        var analysis = await Analyze(broken, execution: execution);

        Assert.Equal(EasyEditVerdict.Invalid, analysis.Verdict);
        Assert.NotNull(analysis.ErrorLine);
        Assert.Null(analysis.Xslt);
        Assert.Empty(execution.Requests);
    }

    [Fact]
    public async Task AnalyzeAsync_EngineUnavailable_Unavailable()
    {
        var analysis = await Analyze(ThemeFixtures.CompanyImagesTheme, execution: new UnavailableExecution());

        Assert.Equal(EasyEditVerdict.Unavailable, analysis.Verdict);
        Assert.Equal(ThemeSlotAnalyzer.EngineUnavailableCode, analysis.ReasonCode);
        Assert.Null(analysis.Xslt);
    }

    private sealed class UnavailableExecution : IXsltExecutionService
    {
        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new XsltRunResult(null, XsltEngineKind.DotNet, null, false, new XsltDiagnostic(
                "Worker kullanılamadı.", 0, 0, XsltFailureKind.EngineUnavailable, XsltEngineKind.DotNet, "WorkerDisabled")));
    }
}
