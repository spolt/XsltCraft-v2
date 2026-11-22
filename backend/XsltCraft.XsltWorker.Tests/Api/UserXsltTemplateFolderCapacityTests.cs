using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Templates;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// Klasör kapasitesi (<see cref="TemplateLimits.MaxTemplatesPerFolder"/>): PATCH folder, toplu yükleme ve bulk-move.
/// Her controller çağrısı ayrı DbContext kullanır (istek kapsamı; bulk-move stub Attach izleme çakışması yaşamasın).
/// </summary>
public sealed class UserXsltTemplateFolderCapacityTests
{
    private const string Xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"/>";
    private const int Capacity = TemplateLimits.MaxTemplatesPerFolder;

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly Guid _folder = Guid.NewGuid();

    public UserXsltTemplateFolderCapacityTests()
    {
        using var db = NewDb();
        db.Users.AddRange(
            new User { Id = Me, Username = "ben", Email = "ben@example.com", PasswordHash = "x" },
            new User { Id = Other, Username = "diger", Email = "diger@example.com", PasswordHash = "x" });
        db.Folders.Add(new Folder { Id = _folder, OwnerId = Me, Name = "Klasor", Kind = FolderKind.XsltTemplate, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task MoveToFolder_FolderFull_Returns409FolderFull()
    {
        await SeedAsync(Me, _folder, Capacity);
        var outside = (await SeedAsync(Me, null, 1)).Single();

        await using var db = NewDb();
        var result = await Controller(db).MoveToFolder(outside, new MoveToFolderRequest { FolderId = _folder });

        AssertFolderFull(result);
        Assert.Null(await FolderOfAsync(outside));
    }

    [Fact]
    public async Task MoveToFolder_AlreadyInFullFolder_Returns204()
    {
        var inside = (await SeedAsync(Me, _folder, Capacity))[0];

        await using var db = NewDb();
        var result = await Controller(db).MoveToFolder(inside, new MoveToFolderRequest { FolderId = _folder });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(_folder, await FolderOfAsync(inside));
    }

    [Fact]
    public async Task BulkUpload_ExceedsCapacity_Returns409AndSavesNothing()
    {
        await SeedAsync(Me, _folder, Capacity - 5);
        var items = Enumerable.Range(1, 10).Select(i => new BulkUploadItem { Name = $"Yeni {i}", XsltContent = Xslt }).ToList();

        await using (var db = NewDb())
        {
            var result = await Controller(db).BulkUpload(new BulkUploadUserXsltRequest { FolderId = _folder, Items = items });
            AssertFolderFull(result);
        }

        await using var verify = NewDb();
        Assert.Equal(Capacity - 5, await verify.UserXsltTemplates.CountAsync());
    }

    [Fact]
    public async Task BulkMove_ExceedsCapacity_Returns409AndMovesNothing()
    {
        await SeedAsync(Me, _folder, Capacity - 2);
        var outside = await SeedAsync(Me, null, 3);

        await using (var db = NewDb())
        {
            var result = await Controller(db).BulkMove(new BulkMoveUserXsltRequest { Ids = outside, FolderId = _folder });
            AssertFolderFull(result);
        }

        foreach (var id in outside)
            Assert.Null(await FolderOfAsync(id));
    }

    [Fact]
    public async Task BulkMove_ForeignId_IsSkipped()
    {
        var mine = (await SeedAsync(Me, null, 1)).Single();
        var foreign = (await SeedAsync(Other, null, 1)).Single();

        await using (var db = NewDb())
        {
            var result = await Controller(db).BulkMove(new BulkMoveUserXsltRequest { Ids = [mine, foreign], FolderId = _folder });
            var moved = Assert.IsType<BulkMoveResultResponse>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal([mine], moved.MovedIds);
        }

        Assert.Equal(_folder, await FolderOfAsync(mine));
        Assert.Null(await FolderOfAsync(foreign));
    }

    [Fact]
    public async Task BulkMove_NullFolder_PersistsRemoval()
    {
        var ids = await SeedAsync(Me, _folder, 2);

        await using (var db = NewDb())
        {
            var result = await Controller(db).BulkMove(new BulkMoveUserXsltRequest { Ids = ids, FolderId = null });
            Assert.IsType<OkObjectResult>(result);
        }

        foreach (var id in ids)
            Assert.Null(await FolderOfAsync(id));
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private async Task<List<Guid>> SeedAsync(Guid owner, Guid? folderId, int count)
    {
        var now = DateTime.UtcNow;
        var templates = Enumerable.Range(0, count).Select(i => new UserXsltTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"T{i}",
            OwnerId = owner,
            XsltContent = "<a/>",
            FolderId = folderId,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

        await using var db = NewDb();
        db.UserXsltTemplates.AddRange(templates);
        await db.SaveChangesAsync();
        return templates.Select(t => t.Id).ToList();
    }

    /// <summary>Yeni DbContext ile kalıcı FolderId (izleme önbelleği yok).</summary>
    private async Task<Guid?> FolderOfAsync(Guid id)
    {
        await using var db = NewDb();
        return await db.UserXsltTemplates.Where(t => t.Id == id).Select(t => t.FolderId).SingleAsync();
    }

    private static void AssertFolderFull(IActionResult result)
    {
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("folder_full", JsonSerializer.SerializeToElement(conflict.Value).GetProperty("error").GetString());
    }

    private static UserXsltTemplateController Controller(AppDbContext db) =>
        new(db, new NoActivity(), new ProEntitlements(), null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me.ToString())], "test")),
                },
            },
        };

    private sealed class NoActivity : IUserActivityRecorder
    {
        public Task RecordAsync(Guid userId, UserActivityType type, Guid? entityId = null, string? entityKind = null) => Task.CompletedTask;
    }

    private sealed class ProEntitlements : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(EntitlementPolicy.Compute(UserRole.User, MembershipPlan.Pro, null, new MembershipOptions(), DateTime.UtcNow));
    }
}
