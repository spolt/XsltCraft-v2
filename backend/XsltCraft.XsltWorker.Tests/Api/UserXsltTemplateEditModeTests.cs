using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary><see cref="UserXsltTemplate.LastEditMode"/>: oluşturma, güncelleme, liste ve geçersiz değer.</summary>
public sealed class UserXsltTemplateEditModeTests : IAsyncDisposable
{
    private const string Xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"/>";
    private static readonly Guid Me = Guid.NewGuid();

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public UserXsltTemplateEditModeTests()
    {
        _db.Users.Add(new User { Id = Me, Username = "kullanici", Email = "k@example.com", PasswordHash = "x" });
        _db.SaveChanges();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Create_WithoutMode_DefaultsToEditor()
    {
        var created = Assert.IsType<CreatedAtActionResult>(await Controller().Create(new CreateUserXsltRequest { XsltContent = Xslt }));

        Assert.Equal("editor", Assert.IsType<UserXsltTemplateDetailResponse>(created.Value).LastEditMode);
        Assert.Equal(XsltEditMode.Editor, (await _db.UserXsltTemplates.SingleAsync()).LastEditMode);
    }

    [Fact]
    public async Task Update_WritesMode_AndNullKeepsIt()
    {
        var id = await CreateAsync();

        var written = Assert.IsType<OkObjectResult>(await Controller().Update(id, new UpdateUserXsltRequest { LastEditMode = "easyEdit" }));
        Assert.Equal("easyEdit", Assert.IsType<UserXsltTemplateDetailResponse>(written.Value).LastEditMode);

        var kept = Assert.IsType<OkObjectResult>(await Controller().Update(id, new UpdateUserXsltRequest { Name = "yeni ad" }));
        Assert.Equal("easyEdit", Assert.IsType<UserXsltTemplateDetailResponse>(kept.Value).LastEditMode);
        Assert.Equal(XsltEditMode.EasyEdit, (await _db.UserXsltTemplates.SingleAsync()).LastEditMode);
    }

    [Fact]
    public async Task GetAll_ReturnsMode()
    {
        await Controller().Create(new CreateUserXsltRequest { XsltContent = Xslt, LastEditMode = "easyEdit" });

        var list = Assert.IsType<OkObjectResult>(await Controller().GetAll());

        Assert.Equal("easyEdit", Assert.Single(Assert.IsType<List<UserXsltTemplateResponse>>(list.Value)).LastEditMode);
    }

    [Fact]
    public async Task InvalidMode_Returns400()
    {
        var id = await CreateAsync();

        Assert.IsType<BadRequestObjectResult>(await Controller().Create(new CreateUserXsltRequest { XsltContent = Xslt, LastEditMode = "EasyEdit" }));
        Assert.IsType<BadRequestObjectResult>(await Controller().Update(id, new UpdateUserXsltRequest { LastEditMode = "grid" }));
        Assert.Equal(XsltEditMode.Editor, (await _db.UserXsltTemplates.SingleAsync()).LastEditMode);
    }

    private async Task<Guid> CreateAsync()
    {
        var created = Assert.IsType<CreatedAtActionResult>(await Controller().Create(new CreateUserXsltRequest { XsltContent = Xslt }));
        return Assert.IsType<UserXsltTemplateDetailResponse>(created.Value).Id;
    }

    private UserXsltTemplateController Controller() =>
        new(_db, new NoActivity(), new ProEntitlements(), null!)
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
