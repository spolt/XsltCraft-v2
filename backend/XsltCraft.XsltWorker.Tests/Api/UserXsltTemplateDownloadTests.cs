using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Templates.Download;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>ZIP indirme uçlarında sahiplik (IDOR): yalnız kendi şablonları; paylaşılan/yabancı/Draft klasör girmez.</summary>
public sealed class UserXsltTemplateDownloadTests : IAsyncDisposable
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public UserXsltTemplateDownloadTests()
    {
        _db.Users.AddRange(
            new User { Id = Me, Username = "ben", Email = "ben@example.com", PasswordHash = "x" },
            new User { Id = Other, Username = "diger", Email = "diger@example.com", PasswordHash = "x" });
        _db.SaveChanges();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task DownloadSelected_MixedIds_ZipContainsOnlyOwnTemplates()
    {
        var mine1 = AddTemplate(Me, "Benim A", "<a>benim-a</a>");
        var mine2 = AddTemplate(Me, "Benim B", "<a>benim-b</a>");
        var foreign = AddTemplate(Other, "Yabanci", "<a>yabanci</a>");
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadSelected(
            new DownloadUserXsltRequest { Ids = [mine1, foreign, Guid.NewGuid(), mine2] }, CancellationToken.None);

        var entries = await ReadZipAsync(result);
        Assert.Equal(["Benim A.xslt", "Benim B.xslt"], entries.Keys.Order());
        Assert.DoesNotContain(entries.Values, c => c.Contains("yabanci"));
    }

    [Fact]
    public async Task DownloadSelected_AllForeignIds_Returns404()
    {
        var foreign = AddTemplate(Other, "Yabanci", "<a/>");
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadSelected(
            new DownloadUserXsltRequest { Ids = [foreign, Guid.NewGuid()] }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadSelected_SharedWithMe_NotIncluded()
    {
        var mine = AddTemplate(Me, "Benim", "<a>benim</a>");
        var shared = AddTemplate(Other, "Paylasilan", "<a>paylasilan</a>");
        _db.UserXsltTemplateShares.Add(new UserXsltTemplateShare
        {
            Id = Guid.NewGuid(),
            TemplateId = shared,
            UserId = Me,
            GrantedByUserId = Other,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadSelected(
            new DownloadUserXsltRequest { Ids = [mine, shared] }, CancellationToken.None);

        var entries = await ReadZipAsync(result);
        Assert.Equal("Benim.xslt", Assert.Single(entries).Key);
    }

    [Fact]
    public async Task DownloadFolder_OtherUsersFolder_Returns404()
    {
        var folder = AddFolder(Other, FolderKind.XsltTemplate);
        AddTemplate(Other, "Yabanci", "<a/>", folder);
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadFolder(folder, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadFolder_DraftKindFolder_Returns404()
    {
        var folder = AddFolder(Me, FolderKind.Draft);
        AddTemplate(Me, "Benim", "<a/>", folder);
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadFolder(folder, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadFolder_NoOwnTemplates_Returns404FolderEmpty()
    {
        var folder = AddFolder(Me, FolderKind.XsltTemplate);
        // Başkasının şablonu bu klasöre işaret etse bile sayılmaz.
        AddTemplate(Other, "Yabanci", "<a/>", folder);
        await _db.SaveChangesAsync();

        var result = await Controller().DownloadFolder(folder, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("folder_empty", JsonSerializer.SerializeToElement(notFound.Value).GetProperty("error").GetString());
    }

    private Guid AddTemplate(Guid owner, string name, string content, Guid? folderId = null)
    {
        var id = Guid.NewGuid();
        _db.UserXsltTemplates.Add(new UserXsltTemplate
        {
            Id = id,
            Name = name,
            OwnerId = owner,
            XsltContent = content,
            FolderId = folderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        return id;
    }

    private Guid AddFolder(Guid owner, FolderKind kind)
    {
        var id = Guid.NewGuid();
        _db.Folders.Add(new Folder { Id = id, OwnerId = owner, Name = "Klasor", Kind = kind, CreatedAt = DateTime.UtcNow });
        return id;
    }

    /// <summary>Sonucu ZIP olarak okur (girdi adı → içerik); geçici dosya akışı dispose edilir (DeleteOnClose).</summary>
    private static async Task<Dictionary<string, string>> ReadZipAsync(IActionResult result)
    {
        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/zip", file.ContentType);

        await using var stream = file.FileStream;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = new Dictionary<string, string>();
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            entries[entry.FullName] = await reader.ReadToEndAsync();
        }
        return entries;
    }

    private UserXsltTemplateDownloadController Controller() =>
        new(_db, new TemplateArchiveWriter(), new NoActivity(), new XsltCraft.Api.Downloads.ArchiveDownloadGate())
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
}
