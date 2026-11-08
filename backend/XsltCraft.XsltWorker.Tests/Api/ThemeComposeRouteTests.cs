using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Themes;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// <c>POST /api/preview/compose</c> önizlemesi: istekle gelen <c>xslt</c> kullanıcı kodudur →
/// <see cref="XsltRoute.Raw"/> (izole); sunucudaki hazır tema (<c>themeId</c>) → <see cref="XsltRoute.Theme"/>.
/// </summary>
public sealed class ThemeComposeRouteTests
{
    private const string Xslt =
        "<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:template match='/'><p>ok</p></xsl:template></xsl:stylesheet>";

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid PublishedTheme = Guid.NewGuid();

    [Fact]
    public async Task ComposeRender_XsltMode_UsesRaw_ThemeIdMode_UsesTheme()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Templates.Add(new Template { Id = PublishedTheme, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/a.xslt" });
        await db.SaveChangesAsync();
        var execution = new RecordingExecution();
        var controller = Controller(db, execution);

        Assert.IsType<OkObjectResult>(await controller.Compose(
            new ThemeComposeRequest { Xslt = Xslt, XmlContent = "<r/>", IncludeHtml = true }, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.Compose(
            new ThemeComposeRequest { ThemeId = PublishedTheme, XmlContent = "<r/>", IncludeHtml = true }, CancellationToken.None));

        Assert.Equal(2, execution.Requests.Count);
        Assert.Equal(XsltRoute.Raw, execution.Requests[0].Route);
        Assert.Equal(XsltRoute.Theme, execution.Requests[1].Route);
    }

    public static TheoryData<string> OverLimitXslt => new()
    {
        string.Concat(Enumerable.Repeat("<b>", ThemeComposeXsltLimits.MaxDepth + 1)) + string.Concat(Enumerable.Repeat("</b>", ThemeComposeXsltLimits.MaxDepth + 1)),
        "<r>" + string.Concat(Enumerable.Repeat("<a/>", ThemeComposeXsltLimits.MaxElements)) + "</r>",
        // Tek etikette öznitelik taşkını (4 MB altında): ayrıştırma ilk Read() içinde biter.
        "<div" + string.Concat(Enumerable.Range(0, 400_000).Select(i => $" a{i:x}=\"\"")) + "/>",
        // XSL ad alanında benzersiz eleman adları: XName tablosunda kalıcı.
        "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>"
            + string.Concat(Enumerable.Range(0, 15_900).Select(i => $"<xsl:n{i}/>")) + "</xsl:stylesheet>",
    };

    [Theory]
    [MemberData(nameof(OverLimitXslt))]
    public async Task Compose_OverLimitXslt_Returns400WithoutComposing_ThemeIdIsNotLimited(string overLimit)
    {
        Assert.True(Encoding.UTF8.GetByteCount(overLimit) <= ThemeComposeRequestValidator.MaxXsltBytes, "Girdi boyut sınırına takılmamalı.");

        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Templates.Add(new Template { Id = PublishedTheme, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/a.xslt" });
        await db.SaveChangesAsync();
        var composer = new SpyComposer();
        var controller = Controller(db, new RecordingExecution(), composer, overLimit);

        var result = await controller.Compose(new ThemeComposeRequest { Xslt = overLimit, IncludeXslt = true }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("xslt", bad.Value!.GetType().GetProperty("field")!.GetValue(bad.Value));
        Assert.Equal(0, composer.Calls);

        // Sunucudaki hazır tema (admin içeriği) bu denetime girmez.
        Assert.IsType<OkObjectResult>(await controller.Compose(
            new ThemeComposeRequest { ThemeId = PublishedTheme, IncludeXslt = true }, CancellationToken.None));
        Assert.Equal(1, composer.Calls);
    }

    [Fact]
    public async Task Compose_GateFull_XsltModeReturns429_ThemeIdPasses_SlotReleasedAfterCompose()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Templates.Add(new Template { Id = PublishedTheme, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/a.xslt" });
        await db.SaveChangesAsync();
        var composer = new SpyComposer();
        var gate = new ThemeComposeGate(1);
        var controller = Controller(db, new RecordingExecution(), composer, gate: gate);

        using (gate.TryEnter())
        {
            var busy = Assert.IsType<ObjectResult>(await controller.Compose(
                new ThemeComposeRequest { Xslt = Xslt, IncludeXslt = true }, CancellationToken.None));
            Assert.Equal(StatusCodes.Status429TooManyRequests, busy.StatusCode);
            Assert.NotNull(busy.Value!.GetType().GetProperty("error")!.GetValue(busy.Value));
            Assert.False(string.IsNullOrEmpty(controller.Response.Headers.RetryAfter));
            Assert.Equal(0, composer.Calls);

            Assert.IsType<OkObjectResult>(await controller.Compose(
                new ThemeComposeRequest { ThemeId = PublishedTheme, IncludeXslt = true }, CancellationToken.None));
            Assert.Equal(1, composer.Calls);
        }

        // Birleştirme bitince yuva geri verilir: art arda iki istek de tek yuvayla geçer.
        for (var i = 0; i < 2; i++)
            Assert.IsType<OkObjectResult>(await controller.Compose(
                new ThemeComposeRequest { Xslt = Xslt, IncludeXslt = true }, CancellationToken.None));
    }

    private const string SlottedXslt =
        "<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:template match='/'>"
        + "<div data-xc-slot=\"seller-top\" data-xc-layout=\"block\" data-xc-inserted=\"1\"></div>"
        + "<div data-xc-slot=\"qr-below\" data-xc-layout=\"block-clear\" data-xc-inserted=\"1\"></div>"
        + "</xsl:template></xsl:stylesheet>";

    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static List<ThemeComposeAssetDto> LogoAndSignature() =>
    [
        new() { Kind = "logo", Slot = "qr-below", DataUrl = Png },
        new() { Kind = "signature", Slot = "seller-top", DataUrl = Png },
    ];

    [Fact]
    public async Task Compose_XsltModeWithConditionalSlots_WarnsForRequestedSlotOnly()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = Controller(db, new RecordingExecution(), storedXslt: SlottedXslt);

        var ok = Assert.IsType<OkObjectResult>(await controller.Compose(new ThemeComposeRequest
        {
            Xslt = SlottedXslt,
            Assets = LogoAndSignature(),
            ConditionalSlots = ["qr-below"],
            IncludeXslt = true,
        }, CancellationToken.None));

        var conditional = Assert.IsType<ThemeComposeResponse>(ok.Value).Warnings.Where(w => w.Code == "slot-conditional").ToList();
        Assert.Equal("logo", Assert.Single(conditional).Kind);
    }

    [Fact]
    public async Task Compose_ThemeIdModeWithConditionalSlots_IgnoresRequestAndUsesCatalog()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var catalog = new ThemeSlotCatalog(ThemeSlotCatalog.CurrentVersion, 1, ThemeSlotStatus.Ok, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [],
            [
                new ThemeSlot("seller-top", "R1", 1, SlotLayout.Block, null, Conditional: true, null, true, 1),
                new ThemeSlot("qr-below", "R2", 2, SlotLayout.BlockClear, null, Conditional: false, null, true, 1),
            ], [], null, []);
        db.Templates.Add(new Template
        {
            Id = PublishedTheme,
            Name = "tema",
            IsFreeTheme = true,
            XsltStoragePath = "themes/a.xslt",
            SlotCatalog = ThemeSlotCatalogJson.Serialize(catalog),
        });
        await db.SaveChangesAsync();
        var controller = Controller(db, new RecordingExecution(), storedXslt: SlottedXslt);

        var ok = Assert.IsType<OkObjectResult>(await controller.Compose(new ThemeComposeRequest
        {
            ThemeId = PublishedTheme,
            Assets = LogoAndSignature(),
            ConditionalSlots = ["qr-below"],
            IncludeXslt = true,
        }, CancellationToken.None));

        var conditional = Assert.IsType<ThemeComposeResponse>(ok.Value).Warnings.Where(w => w.Code == "slot-conditional").ToList();
        Assert.Equal("signature", Assert.Single(conditional).Kind);
    }

    private static ThemeComposeController Controller(AppDbContext db, IXsltExecutionService execution,
        IThemeAssetComposer? composer = null, string? storedXslt = null, IThemeComposeGate? gate = null) =>
        new(db, new XsltStorage(storedXslt ?? Xslt), new ThemeAccessGuard(new NoEntitlements()), composer ?? new ThemeAssetComposer(new ThemeImageValidator()),
            new ThemeSlotProjector(), execution, gate ?? new ThemeComposeGate(2), NullLogger<ThemeComposeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me.ToString())], "test")),
                },
            },
        };

    private sealed class RecordingExecution : IXsltExecutionService
    {
        public List<XsltRunRequest> Requests { get; } = [];

        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new XsltRunResult("<p>ok</p>", XsltEngineKind.DotNet, null, false, null));
        }
    }

    private sealed class SpyComposer : IThemeAssetComposer
    {
        public int Calls { get; private set; }

        public ThemeComposeResult Compose(string xslt, ThemeComposeInput input)
        {
            Calls++;
            return new ThemeComposeResult(true, xslt, [], new Dictionary<ThemeAssetKind, string?>(), 0, null);
        }
    }

    private sealed class XsltStorage(string xslt) : IStorageService
    {
        public Task<string> WriteAsync(Stream content, string relativePath, string contentType) => Task.FromResult(relativePath);
        public Task<Stream> ReadAsync(string relativePath) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(xslt)));
        public Task DeleteAsync(string relativePath) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(true);
    }

    private sealed class NoEntitlements : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new UserEntitlements(UserRole.User, MembershipPlan.Free, false, false, false, false, false, 0, 0, 0, 0));
    }
}
