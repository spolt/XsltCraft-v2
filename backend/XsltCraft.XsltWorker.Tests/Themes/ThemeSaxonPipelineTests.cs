using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Themes;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Tests.Engine;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Themes;

/// <summary>
/// T-izi: tema hattı gerçek <see cref="IXsltExecutionService"/> + worker havuzuyla. GİB <c>general.xslt</c>
/// (XSLT 2.0, <c>exists()</c>) yüklenir, yuvaları Saxon serileştirmesiyle doğrulanır, compose ve galeri önizlemesi
/// Saxon'la render eder; 2.0 yapısı kullanmayan temalar .NET'te kalır ve çıktıları <see cref="XsltHtmlRenderer"/> ile
/// bit-aynıdır.
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class ThemeSaxonPipelineTests(ThemeSaxonPipelineTests.Engines engines) : IClassFixture<ThemeSaxonPipelineTests.Engines>
{
    private const string InvoiceNumber = "TST2026000000001";
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly IThemeSampleXmlProvider Samples = new ThemeSampleXmlProvider();

    private IXsltExecutionService Xslt => engines.Service;

    [Fact]
    public async Task Upload_GibGeneral_IsAccepted_AndSlotsVerifiedWithSaxon()
    {
        var ready = await UploadGibGeneralAsync();

        Assert.NotEqual(ThemeSlotStatus.Failed, ready.Catalog.Status);
        Assert.NotEmpty(ready.Catalog.Slots);
        Assert.DoesNotContain(ready.Catalog.Warnings, w => w.Code is "render-failed" or "render-mismatch" or "compile-failed");

        // İşaretli tema Saxon'la (izole) render olur; koşulsuz her yuva çıktıda tam bir kez.
        var marked = await RenderAsync(ready.Xslt);
        Assert.Equal((XsltEngineKind.Saxon, XsltExecutionPlace.Isolated), (marked.Engine, marked.Place));
        Assert.Contains(InvoiceNumber, marked.Html);
        foreach (var slot in ready.Catalog.Slots.Where(s => !s.Conditional))
            Assert.Single(Regex.Matches(marked.Html!, $"data-xc-slot=\"{Regex.Escape(slot.Id)}\""));

        // Değişmez aynı motorla: işaretsiz render da Saxon.
        var source = await RenderAsync(EngineTestKit.GibGeneral);
        Assert.Equal(XsltEngineKind.Saxon, source.Engine);
    }

    [Fact]
    public async Task Compose_And_PreviewTheme_GibGeneral_RenderWithSaxon()
    {
        var ready = await UploadGibGeneralAsync();

        // Kullanıcı compose: logo + banka tablosu + ThemeAssetRuntime.js enjeksiyonu Saxon'da da çalışır.
        var compose = ComposeController(Admin);
        var composed = Assert.IsType<OkObjectResult>(await compose.Compose(new ThemeComposeRequest
        {
            Xslt = ready.Xslt,
            XmlContent = Samples.Get(DocumentType.Invoice),
            Assets = [new ThemeComposeAssetDto { Kind = "logo", DataUrl = Png, Width = 120, Height = 60 }],
            BankInfo = [new BankInfoItem { BankName = "Test Bankası", Iban = "TR000000000000000000000001" }],
            IncludeHtml = true,
            IncludeXslt = true,
        }, CancellationToken.None));
        var response = Assert.IsType<ThemeComposeResponse>(composed.Value);
        Assert.Contains(InvoiceNumber, response.Html);
        Assert.Contains("data-xc-overrides=\"2\"", response.Html);
        Assert.Contains(Png, response.Html);
        Assert.Contains("TR000000000000000000000001", response.Html);
        Assert.Equal(XsltEngineKind.Saxon, (await RenderAsync(response.Xslt!)).Engine);

        // Galeri önizlemesi (anonim, yayınlanmış tema).
        var (db, themeId) = await SeedThemeAsync();
        await using var _ = db;
        var preview = Assert.IsType<OkObjectResult>(
            await PreviewController(db, EngineTestKit.GibGeneral).PreviewTheme(themeId, new ThemePreviewRequest { XmlContent = Samples.Get(DocumentType.Invoice) }));
        var body = JsonSerializer.SerializeToElement(preview.Value);
        Assert.Equal("Saxon", body.GetProperty("engine").GetString());
        Assert.Contains(InvoiceNumber, body.GetProperty("html").GetString());
    }

    [Theory]
    [InlineData("test-theme.xslt", DocumentType.Invoice)]
    [InlineData("gib-einvoice.xslt", DocumentType.Invoice)]
    [InlineData("gib-template.xslt", DocumentType.Invoice)]
    [InlineData("gib-eirsaliye.xslt", DocumentType.Despatch)]
    public async Task ThemesWithoutXslt2Constructs_StayOnDotNet_BitIdentical(string fixture, DocumentType type)
    {
        var xslt = Fixture(fixture);
        var xml = Samples.Get(type);

        var result = await RenderAsync(xslt, xml);

        Assert.Equal((XsltEngineKind.DotNet, XsltExecutionPlace.InProcess), (result.Engine, result.Place));
        Assert.Equal(new XsltHtmlRenderer().Render(xslt, xml).Html, result.Html);
    }

    [Fact]
    public async Task Upload_Xslt30Theme_RejectedWithTurkishMessage()
    {
        const string xslt = """
            <xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/"><html><body><p>x</p></body></html></xsl:template>
            </xsl:stylesheet>
            """;

        var outcome = await Processor().ProcessAsync(xslt, DocumentType.Invoice, null, null, Admin);

        var rejected = Assert.IsType<ThemeUploadOutcome.Rejected>(outcome);
        Assert.Contains("desteklenmiyor", rejected.Message);
        Assert.Contains("en fazla XSLT 2.0", rejected.Message);
    }

    // ------------------------------------------------------------------ yardımcılar

    private async Task<ThemeUploadOutcome.Ready> UploadGibGeneralAsync()
    {
        var processor = Processor();
        var outcome = await processor.ProcessAsync(EngineTestKit.GibGeneral, DocumentType.Invoice, null, null, Admin);
        if (outcome is ThemeUploadOutcome.DecisionRequired required)
        {
            var roles = required.Images.Select(_ => new ThemeImageDecision(ThemeImageRole.Design, false)).ToList();
            outcome = await processor.ProcessAsync(EngineTestKit.GibGeneral, DocumentType.Invoice,
                required.Inspection is null ? null : OverridesDecision.Strip, roles, Admin);
        }
        return Assert.IsType<ThemeUploadOutcome.Ready>(outcome);
    }

    private ThemeUploadProcessor Processor() =>
        new(new ThemeOverridesProcessor(new ThemeImageValidator()), new ThemeSlotAnalyzer(Xslt, Samples), Xslt, Samples);

    private Task<XsltRunResult> RenderAsync(string xslt, string? xml = null) =>
        Xslt.ExecuteAsync(new XsltRunRequest(XsltRoute.Theme, xslt, xml ?? Samples.Get(DocumentType.Invoice), XsltOperation.Render, Admin));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(EngineTestKit.BackendDir(), "XsltCraft.Application.Tests", "ThemeSlots", "Fixtures", name));

    private ThemeComposeController ComposeController(Guid user)
    {
        var controller = new ThemeComposeController(null!, null!, null!, new ThemeAssetComposer(new ThemeImageValidator()),
            new ThemeSlotProjector(), Xslt, NullLogger<ThemeComposeController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test")),
            },
        };
        return controller;
    }

    private PreviewController PreviewController(AppDbContext db, string themeXslt)
    {
        var controller = new PreviewController(null!, new TextStorage(themeXslt), db, null!, null!, Xslt, null!,
            new ThemeAccessGuard(new FreeEntitlements()), NullLogger<PreviewController>.Instance);
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static async Task<(AppDbContext Db, Guid Id)> SeedThemeAsync()
    {
        var id = Guid.NewGuid();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Templates.Add(new Template { Id = id, Name = "general", IsFreeTheme = true, XsltStoragePath = "themes/general.xslt" });
        await db.SaveChangesAsync();
        return (db, id);
    }

    private sealed class TextStorage(string text) : IStorageService
    {
        public Task<string> WriteAsync(Stream content, string relativePath, string contentType) => Task.FromResult(relativePath);
        public Task<Stream> ReadAsync(string relativePath) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        public Task DeleteAsync(string relativePath) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(true);
    }

    private sealed class FreeEntitlements : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new UserEntitlements(UserRole.User, MembershipPlan.Free, false, false, false, false, false, 0, 0, 0, 0));
    }

    /// <summary>Sınıf başı tek gerçek yürütme hattı (1 worker).</summary>
    public sealed class Engines : IAsyncLifetime
    {
        private ServiceProvider? _provider;

        public IXsltExecutionService Service { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XsltWorker:PoolSize"] = "1",
                ["XsltWorker:StartupTimeoutMs"] = "20000",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddXsltExecution(configuration);
            _provider = services.BuildServiceProvider();

            var pool = _provider.GetRequiredService<XsltWorkerPool>();
            await pool.StartAsync(CancellationToken.None);
            Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => pool.ReadyCount >= 1, TimeSpan.FromSeconds(30)), "worker hazır değil");
            Service = _provider.GetRequiredService<IXsltExecutionService>();
        }

        public async Task DisposeAsync()
        {
            if (_provider is not null)
                await _provider.DisposeAsync();
        }
    }
}
