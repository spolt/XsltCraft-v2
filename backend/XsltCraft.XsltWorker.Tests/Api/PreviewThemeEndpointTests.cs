using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using XsltCraft.Api.Controllers;
using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Themes;
using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// G3 Y2: <c>POST /api/preview/theme/{id}</c> — yalnız erişilebilir yayınlanmış tema (<see cref="ThemeAccessGuard"/>)
/// <see cref="XsltRoute.Theme"/>'dan, sahibin kendi saklı şablonu <see cref="XsltRoute.UserTemplate"/>'ten; girdi
/// grid sınırlarıyla (XML 5 MB) servis içinde reddedilir.
/// </summary>
public sealed class PreviewThemeEndpointTests
{
    private const string Xslt =
        "<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:template match='/'><p>ok</p></xsl:template></xsl:stylesheet>";

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid PublishedTheme = Guid.NewGuid();
    private static readonly Guid UnpublishedTheme = Guid.NewGuid();
    private static readonly Guid OthersTemplate = Guid.NewGuid();
    private static readonly Guid MyTemplate = Guid.NewGuid();

    [Fact]
    public async Task PreviewTheme_UnpublishedOrUserTemplate_Forbidden()
    {
        var xslt = new RecordingExecution();
        await using var db = await SeedAsync();

        foreach (var (id, user) in new[] { (UnpublishedTheme, (Guid?)null), (UnpublishedTheme, Me), (OthersTemplate, null), (OthersTemplate, Me) })
        {
            var result = await Controller(db, xslt, user).PreviewTheme(id, new ThemePreviewRequest { XmlContent = "<r/>" });
            Assert.IsType<NotFoundObjectResult>(result);
        }
        Assert.Empty(xslt.Requests); // hiçbiri yürütülmedi

        // Yayınlanmış tema (anonim) → Theme rotası, IP'den türetilmiş kapı anahtarı.
        Assert.IsType<OkObjectResult>(await Controller(db, xslt, null).PreviewTheme(PublishedTheme, new ThemePreviewRequest { XmlContent = "<r/>" }));
        var theme = Assert.Single(xslt.Requests);
        Assert.Equal(XsltRoute.Theme, theme.Route);
        Assert.NotEqual(Guid.Empty, theme.UserId);

        // Sahibin kendi saklı şablonu → izole UserTemplate rotası.
        Assert.IsType<OkObjectResult>(await Controller(db, xslt, Me).PreviewTheme(MyTemplate, new ThemePreviewRequest { XmlContent = "<r/>" }));
        Assert.Equal((XsltRoute.UserTemplate, Me), (xslt.Requests[1].Route, xslt.Requests[1].UserId));
    }

    [Fact]
    public async Task PreviewTheme_OversizeXml_Rejected()
    {
        await using var db = await SeedAsync();
        var service = new XsltExecutionService(new XsltExecutionOptions(), new DefaultXsltSafetyScanner(),
            new ThrowingGate(), null!, null!); // sınır kapıdan/host'tan önce reddeder
        var oversize = "<r>" + new string('x', 6_000_001) + "</r>";

        var result = Assert.IsType<ObjectResult>(
            await Controller(db, service, null).PreviewTheme(PublishedTheme, new ThemePreviewRequest { XmlContent = oversize }));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var body = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("LimitExceeded", body.GetProperty("kind").GetString());
        Assert.Equal("XmlTooLarge", body.GetProperty("code").GetString());

        // Gövde sınırı ve IP bölümlü hız sınırı uçta bildirilir.
        var method = typeof(PreviewController).GetMethod(nameof(PreviewController.PreviewTheme))!;
        Assert.Equal(6 * 1024 * 1024, ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)method.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
        Assert.Equal(RateLimitPolicies.ThemePreview, method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Templates.AddRange(
            new Template { Id = PublishedTheme, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/a.xslt" },
            new Template { Id = UnpublishedTheme, Name = "taslak", IsFreeTheme = false, XsltStoragePath = "themes/b.xslt" },
            new Template { Id = OthersTemplate, Name = "başkası", OwnerId = Other, XsltStoragePath = "templates/o.xslt" },
            new Template { Id = MyTemplate, Name = "benim", OwnerId = Me, XsltStoragePath = "templates/m.xslt" });
        await db.SaveChangesAsync();
        return db;
    }

    private static PreviewController Controller(AppDbContext db, IXsltExecutionService xslt, Guid? user)
    {
        var controller = new PreviewController(null!, new XsltStorage(), db, null!, null!, xslt, null!,
            new ThemeAccessGuard(new NoEntitlements()), NullLogger<PreviewController>.Instance);
        var principal = user is { } id
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        var http = new DefaultHttpContext { User = principal };
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private sealed class RecordingExecution : IXsltExecutionService
    {
        public List<XsltRunRequest> Requests { get; } = [];

        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new XsltRunResult("<p>ok</p>", XsltEngineKind.DotNet, null, false, null));
        }
    }

    private sealed class XsltStorage : IStorageService
    {
        public Task<string> WriteAsync(Stream content, string relativePath, string contentType) => Task.FromResult(relativePath);
        public Task<Stream> ReadAsync(string relativePath) => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Xslt)));
        public Task DeleteAsync(string relativePath) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(true);
    }

    private sealed class NoEntitlements : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new UserEntitlements(UserRole.User, MembershipPlan.Free, false, false, false, false, false, 0, 0, 0, 0));
    }

    private sealed class ThrowingGate : IXsltUserGate
    {
        public bool TryAcquire(Guid userId, XsltGateLane lane, XsltExecutionPlace place,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IXsltGateLease? lease,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out XsltGateDenial? denial) =>
            throw new InvalidOperationException("Sınır aşımında kapıya ulaşılmamalı.");
    }
}
