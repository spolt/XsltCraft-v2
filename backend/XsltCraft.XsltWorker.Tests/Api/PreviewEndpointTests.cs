using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using XsltCraft.Api.Controllers;
using XsltCraft.Api.Preview;
using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Themes;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Tests.Engine;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>T3.1 (a): önizleme/doğrulama uçları kimliksiz isteğe 401 döner (gerçek Kestrel + MVC yetkilendirmesi).</summary>
public sealed class PreviewAuthorizationTests
{
    [Theory]
    [InlineData("/api/preview/raw", """{"xslt":"<x/>","xmlContent":"<r/>"}""")]
    [InlineData("/api/preview/validate-xslt", """{"xslt":"<x/>"}""")]
    [InlineData("/api/preview", """{"version":2,"blocks":{},"xmlContent":"<r/>"}""")]
    [InlineData("/api/preview/user-template/6f1c2f3e-1111-4222-8333-444455556666", """{"xmlContent":"<r/>"}""")]
    public async Task Anonymous_Returns401(string path, string body)
    {
        await using var app = await StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };

        using var response = await client.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(PreviewController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            o.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))),
                ValidateIssuer = false,
                ValidateAudience = false,
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
}

/// <summary>T3.1 (b): asset IDOR — başkasının asset'i önizlemeye gömülmez (yetkili kullanıcı hariç).</summary>
public sealed class PreviewAssetEmbedderTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid MyAsset = Guid.NewGuid();
    private static readonly Guid OtherAsset = Guid.NewGuid();

    [Fact]
    public async Task OtherUsersAsset_IsNotEmbedded()
    {
        var embedded = await BuildAsync(privileged: false);

        Assert.True(embedded.ContainsKey(MyAsset.ToString()));
        Assert.StartsWith("data:image/png;base64,", embedded[MyAsset.ToString()]);
        Assert.False(embedded.ContainsKey(OtherAsset.ToString()), "Başkasının asset'i gömülmemeli (IDOR).");
    }

    [Fact]
    public async Task PrivilegedUser_EmbedsAnyAsset()
    {
        var embedded = await BuildAsync(privileged: true);

        Assert.True(embedded.ContainsKey(MyAsset.ToString()));
        Assert.True(embedded.ContainsKey(OtherAsset.ToString()));
    }

    private static async Task<Dictionary<string, string>> BuildAsync(bool privileged)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Assets.AddRange(
            new Asset { Id = MyAsset, OwnerId = Me, FilePath = "assets/me.png", MimeType = "image/png" },
            new Asset { Id = OtherAsset, OwnerId = Other, FilePath = "assets/other.png", MimeType = "image/png" });
        await db.SaveChangesAsync();

        var embedder = new PreviewAssetEmbedder(db, new FixedBytesStorage(), new FixedEntitlements(privileged), new ThemeAccessGuard(new FixedEntitlements(privileged)));
        var blocks = new Dictionary<string, BlockDto>
        {
            ["a"] = Image("a", MyAsset),
            ["b"] = Image("b", OtherAsset),
        };
        return await embedder.BuildAsync(blocks, Me);
    }

    private static BlockDto Image(string id, Guid assetId) => new()
    {
        Id = id,
        Type = "Image",
        Config = JsonDocument.Parse($$"""{"assetId":"{{assetId}}"}""").RootElement.Clone(),
    };

    private sealed class FixedEntitlements(bool privileged) : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new UserEntitlements(privileged ? UserRole.Admin : UserRole.User, MembershipPlan.Free,
                privileged, false, false, false, false, 0, 0, 0, 0));
    }
}

/// <summary>T3.1 (c): <see cref="XsltFailureKind"/> → HTTP durum tablosu (+ validate sözleşmesi, Retry-After).</summary>
public sealed class XsltProblemResultsTests
{
    public static TheoryData<XsltFailureKind, int> Table() => new()
    {
        { XsltFailureKind.Xml, 400 },
        { XsltFailureKind.Compile, 400 },
        { XsltFailureKind.Runtime, 400 },
        { XsltFailureKind.Security, 400 },
        { XsltFailureKind.Unsupported, 400 },
        { XsltFailureKind.LimitExceeded, 400 },
        { XsltFailureKind.OutputLimit, 400 },
        { XsltFailureKind.Timeout, 400 },
        { XsltFailureKind.MemoryLimit, 400 },
        { XsltFailureKind.EngineCrashed, 400 },
        { XsltFailureKind.Busy, 503 },
        { XsltFailureKind.EngineUnavailable, 503 },
        { XsltFailureKind.UserBusy, 429 },
        { XsltFailureKind.EngineInternal, 500 },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Kind_MapsToStatus_RenderAndValidate(XsltFailureKind kind, int status)
    {
        var failed = new XsltRunResult(null, XsltEngineKind.Saxon, null, false,
            new XsltDiagnostic("iç ayrıntı /etc/secret", 7, 3, kind, XsltEngineKind.Saxon)) { RetryAfter = TimeSpan.FromSeconds(30) };

        var render = new DefaultHttpContext().Response;
        var renderResult = Assert.IsType<ObjectResult>(XsltProblemResults.Render(failed, 5, render));
        var body = Json(renderResult.Value);
        Assert.Equal(status, renderResult.StatusCode);
        Assert.Equal(kind.ToString(), body.GetProperty("kind").GetString());
        Assert.Equal("Saxon", body.GetProperty("engine").GetString());
        Assert.True(body.TryGetProperty("error", out _) && body.TryGetProperty("line", out _) && body.TryGetProperty("column", out _));
        if (status == 500)
            Assert.DoesNotContain("secret", body.GetProperty("error").GetString());
        Assert.Equal(status is 503 or 429, render.Headers.ContainsKey("Retry-After"));

        // validate-xslt: içerik hatası → 200 { valid:false }, operasyonel → aynı durum kodu.
        var validate = Assert.IsAssignableFrom<ObjectResult>(XsltProblemResults.Validate(failed, new DefaultHttpContext().Response));
        var validateBody = Json(validate.Value);
        if (status == 400)
        {
            Assert.Equal(200, validate.StatusCode);
            Assert.False(validateBody.GetProperty("valid").GetBoolean());
            Assert.Equal(7, validateBody.GetProperty("line").GetInt32());
        }
        else
        {
            Assert.Equal(status, validate.StatusCode);
        }
    }

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
}

/// <summary>
/// T3.1 (d): kullanıcının asıl sorunu — GİB <c>general.xslt</c> (XSLT 2.0, <c>exists()</c>) <c>/api/preview/raw</c>'da
/// gerçek worker havuzu (Saxon) ile 200 + fatura no. Controller gerçek <see cref="IXsltExecutionService"/>'le çağrılır.
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class PreviewRawGibGeneralTests
{
    [Fact]
    public async Task PreviewRaw_GibGeneral_Returns200_WithInvoiceNumber()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XsltWorker:PoolSize"] = "1",
            ["XsltWorker:StartupTimeoutMs"] = "20000",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXsltExecution(configuration);
        await using var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<XsltWorkerPool>();
        await pool.StartAsync(CancellationToken.None);
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => pool.ReadyCount >= 1, TimeSpan.FromSeconds(30)), "worker hazır değil");

        var controller = Controller(provider.GetRequiredService<IXsltExecutionService>());

        var render = await controller.PreviewRaw(new RawPreviewRequest { Xslt = EngineTestKit.GibGeneral, XmlContent = EngineTestKit.InvoiceXml });
        var ok = Assert.IsType<OkObjectResult>(render);
        var body = JsonSerializer.SerializeToElement(ok.Value);
        Assert.Contains("TST2026000000001", body.GetProperty("html").GetString());
        Assert.Equal("Saxon", body.GetProperty("engine").GetString());
        Assert.True(body.TryGetProperty("cacheHit", out _));
        Assert.True(body.GetProperty("timings").TryGetProperty("transformMs", out _));

        var validate = Assert.IsType<OkObjectResult>(await controller.ValidateXslt(new ValidateXsltRequest { Xslt = EngineTestKit.GibGeneral }));
        Assert.True(JsonSerializer.SerializeToElement(validate.Value).GetProperty("valid").GetBoolean());
    }

    private static PreviewController Controller(IXsltExecutionService xslt)
    {
        // PreviewRaw/ValidateXslt yalnız servisi ve logger'ı kullanır; diğer bağımlılıklar bu uçlarda dokunulmaz.
        var controller = new PreviewController(null!, null!, null!, null!, null!, xslt, null!, null!,
            NullLogger<PreviewController>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }
}
