using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using XsltCraft.Api.Preview;
using XsltCraft.Controllers;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// Klon, temanın XSLT dosyasını referans alır: hesabı silinen kullanıcı temanın dosyasını silememeli,
/// yalnız kendi üretilmiş önbelleği silinir.
/// </summary>
public sealed class AccountDeletionStorageTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private const string ThemePath = "themes/t.xslt";

    [Fact]
    public async Task DeleteAccount_KeepsThemeFileReferencedByClone_DeletesOwnCache()
    {
        var themeId = Guid.NewGuid();
        var cloneId = Guid.NewGuid();
        var ownId = Guid.NewGuid();
        var ownPath = GeneratedXsltCache.PathFor(ownId);
        var storage = new MemoryStorage(ThemePath, ownPath);

        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = Me, Username = "kullanici", Email = "k@example.com", PasswordHash = "x" });
        db.Templates.AddRange(
            new Template { Id = themeId, Name = "tema", IsFreeTheme = true, XsltStoragePath = ThemePath },
            new Template { Id = cloneId, Name = "tema (kopya)", OwnerId = Me, XsltStoragePath = ThemePath },
            new Template { Id = ownId, Name = "benim", OwnerId = Me, XsltStoragePath = ownPath });
        await db.SaveChangesAsync();

        var controller = new AuthController(db, null!, null!, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me.ToString())], "test")),
                },
            },
        };

        Assert.IsType<NoContentResult>(await controller.DeleteAccount(storage, db));

        Assert.Contains(ThemePath, storage.Files);
        Assert.DoesNotContain(ownPath, storage.Files);
        Assert.NotNull(await db.Templates.FindAsync(themeId));
        Assert.Null(await db.Templates.FindAsync(cloneId));
        Assert.Null(await db.Templates.FindAsync(ownId));
    }

    private sealed class MemoryStorage(params string[] files) : IStorageService
    {
        public HashSet<string> Files { get; } = [.. files];

        public Task<string> WriteAsync(Stream content, string relativePath, string contentType) => throw new NotSupportedException();
        public Task<Stream> ReadAsync(string relativePath) => throw new NotSupportedException();

        public Task DeleteAsync(string relativePath)
        {
            Files.Remove(relativePath);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(Files.Contains(relativePath));
    }
}
