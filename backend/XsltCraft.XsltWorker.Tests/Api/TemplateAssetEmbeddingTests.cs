using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XsltCraft.Api.Controllers;
using XsltCraft.Api.Preview;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Themes;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// Sahip kuralı: kayıtlı şablon indirmesinde görseller her zaman base64 gömülüdür, hiçbir URL'ye düşülmez.
/// Ücretsiz temadan klonlanan şablondaki tema sahibinin (admin) asset'i gömülür; başkasının özel asset'i gömülmez.
/// </summary>
public sealed class TemplateAssetEmbeddingTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid ThemeId = Guid.NewGuid();
    private static readonly Guid CloneId = Guid.NewGuid();
    private static readonly Guid AdminAsset = Guid.NewGuid();
    private static readonly Guid ForeignAsset = Guid.NewGuid();

    [Fact]
    public async Task ClonedFreeTheme_AdminAsset_EmbeddedAsBase64()
    {
        var storage = new MemoryStorage();
        var legacyPath = $"templates/{CloneId}.xslt";
        storage.Files[legacyPath] = Encoding.UTF8.GetBytes($"<img src=\"/api/assets/{AdminAsset}/serve\"/>");

        await using var db = Db();
        db.Assets.Add(new Asset { Id = AdminAsset, OwnerId = Admin, FilePath = "assets/logo.png", MimeType = "image/png" });
        db.Templates.AddRange(
            new Template { Id = ThemeId, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/t.xslt", BlockTree = Tree(AdminAsset) },
            new Template { Id = CloneId, Name = "tema (kopya)", OwnerId = Me, BlockTree = Tree(AdminAsset), XsltStoragePath = legacyPath });
        await db.SaveChangesAsync();

        var xslt = await DownloadAsync(db, storage);

        Assert.Contains("<img src=\"data:image/png;base64,", xslt);
        Assert.DoesNotContain("/api/assets", xslt);
        Assert.False(storage.Files.ContainsKey(legacyPath), "Eski sürüm (URL'li) önbellek silinmeli.");
        Assert.Equal(GeneratedXsltCache.PathFor(CloneId), (await db.Templates.FindAsync(CloneId))!.XsltStoragePath);
    }

    [Fact]
    public async Task ForeignPrivateAsset_NotEmbedded_NoUrl()
    {
        var storage = new MemoryStorage();
        await using var db = Db();
        db.Assets.Add(new Asset { Id = ForeignAsset, OwnerId = Other, FilePath = "assets/private.png", MimeType = "image/png" });
        db.Templates.AddRange(
            new Template { Id = ThemeId, Name = "tema", IsFreeTheme = true, XsltStoragePath = "themes/t.xslt", BlockTree = Tree(AdminAsset) },
            new Template { Id = CloneId, Name = "benim", OwnerId = Me, BlockTree = Tree(ForeignAsset) });
        await db.SaveChangesAsync();

        var xslt = await DownloadAsync(db, storage);

        Assert.DoesNotContain("/api/assets", xslt);
        Assert.DoesNotContain("data:image/png", xslt);
        Assert.DoesNotContain("<img", xslt);
    }

    private static async Task<string> DownloadAsync(AppDbContext db, MemoryStorage storage)
    {
        var entitlements = new UserEntitlementsStub();
        var controller = new TemplateController(db, storage, new XsltGeneratorService(), new NoActivity(), new AllowQuota(),
            entitlements, new ThemeSlotProjector(),
            new PreviewAssetEmbedder(db, storage, entitlements, new ThemeAccessGuard(entitlements)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me.ToString())], "test")),
                },
            },
        };

        var file = Assert.IsType<FileStreamResult>(await controller.Download(CloneId));
        using var reader = new StreamReader(file.FileStream);
        return await reader.ReadToEndAsync();
    }

    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string Tree(Guid assetId) => """
        {"version":2,"documentType":"Invoice","blocks":{"img1":{"id":"img1","type":"Image",
          "config":{"assetId":"ASSET","assetType":"logo","alignment":"left"},
          "gridLayout":{"x":10,"y":10,"width":40,"height":20,"autoHeight":false}}}}
        """.Replace("ASSET", assetId.ToString());

    private sealed class MemoryStorage : IStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task<string> WriteAsync(Stream content, string relativePath, string contentType)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            Files[relativePath] = ms.ToArray();
            return relativePath;
        }

        public Task<Stream> ReadAsync(string relativePath) =>
            Task.FromResult<Stream>(new MemoryStream(Files.TryGetValue(relativePath, out var bytes) ? bytes : [137, 80, 78, 71]));

        public Task DeleteAsync(string relativePath)
        {
            Files.Remove(relativePath);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath) =>
            Task.FromResult(relativePath.StartsWith("assets/", StringComparison.Ordinal) || Files.ContainsKey(relativePath));
    }

    private sealed class UserEntitlementsStub : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new UserEntitlements(UserRole.User, MembershipPlan.Pro, false, false, false, false, false, 0, 0, 0, 0));
    }

    private sealed class NoActivity : IUserActivityRecorder
    {
        public Task RecordAsync(Guid userId, UserActivityType type, Guid? entityId = null, string? entityKind = null) => Task.CompletedTask;
    }

    private sealed class AllowQuota : IUsageQuotaService
    {
        public Task<QuotaCheck> CheckAiAllowedAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new QuotaCheck(true));
        public Task IncrementAiAsync(Guid userId, int approximateTokens, CancellationToken ct = default) => Task.CompletedTask;
        public Task<QuotaCheck> CheckTemplateExportAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new QuotaCheck(true));
        public Task IncrementTemplateExportAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<DailyUsage> GetUsageAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAiUsageSummary>> GetDailyUsageAsync(DateOnly date, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
