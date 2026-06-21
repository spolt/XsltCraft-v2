using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/folders")]
[Authorize]
public class FolderController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private const int MaxNameLength = 100; // AppDbContext: Folder.Name HasMaxLength(100)

    // Frontend (folderColors.ts) ile birebir aynı allowlist. Bilinmeyen değer → null (varsayılan ton).
    private static readonly HashSet<string> AllowedColors =
        new(StringComparer.OrdinalIgnoreCase) { "blue", "emerald", "amber", "rose", "violet", "slate" };

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        var trimmed = color.Trim();
        return AllowedColors.Contains(trimmed) ? trimmed.ToLowerInvariant() : null;
    }

    private static FolderResponse ToResponse(Folder f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Kind = f.Kind.ToString(),
        Color = f.Color,
        CreatedAt = f.CreatedAt
    };

    // GET /api/folders?kind=Draft|XsltTemplate — kullanıcının kendi klasörleri (ada göre)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string kind)
    {
        if (!Enum.TryParse<FolderKind>(kind, ignoreCase: true, out var folderKind))
            return BadRequest(new { message = "Geçersiz kind. 'Draft' veya 'XsltTemplate' olmalı." });

        var userId = CurrentUserId;
        var folders = await db.Folders
            .Where(f => f.OwnerId == userId && f.Kind == folderKind)
            .OrderBy(f => f.Name)
            .Select(f => new FolderResponse
            {
                Id = f.Id,
                Name = f.Name,
                Kind = f.Kind.ToString(),
                Color = f.Color,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        return Ok(folders);
    }

    // POST /api/folders — yeni klasör oluştur
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Klasör adı boş olamaz." });
        if (request.Name.Trim().Length > MaxNameLength)
            return BadRequest(new { message = $"Klasör adı en fazla {MaxNameLength} karakter olabilir." });
        if (!Enum.TryParse<FolderKind>(request.Kind, ignoreCase: true, out var folderKind))
            return BadRequest(new { message = "Geçersiz kind. 'Draft' veya 'XsltTemplate' olmalı." });

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            OwnerId = CurrentUserId,
            Name = request.Name.Trim(),
            Kind = folderKind,
            Color = NormalizeColor(request.Color),
            CreatedAt = DateTime.UtcNow
        };

        db.Folders.Add(folder);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { kind = folder.Kind.ToString() }, ToResponse(folder));
    }

    // PUT /api/folders/:id — klasörü yeniden adlandır / renklendir (yalnız sahip)
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request)
    {
        var folder = await db.Folders.FindAsync(id);
        if (folder is null)
            return NotFound(new { message = "Klasör bulunamadı." });
        if (folder.OwnerId != CurrentUserId)
            return Forbid();

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Klasör adı boş olamaz." });
            if (request.Name.Trim().Length > MaxNameLength)
                return BadRequest(new { message = $"Klasör adı en fazla {MaxNameLength} karakter olabilir." });
            folder.Name = request.Name.Trim();
        }
        if (request.Color is not null)
            folder.Color = NormalizeColor(request.Color);

        await db.SaveChangesAsync();
        return Ok(ToResponse(folder));
    }

    // DELETE /api/folders/:id — klasörü sil (yalnız sahip). Şablonlar silinmez; FolderId SetNull olur.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var folder = await db.Folders.FindAsync(id);
        if (folder is null)
            return NotFound(new { message = "Klasör bulunamadı." });
        if (folder.OwnerId != CurrentUserId)
            return Forbid();

        db.Folders.Remove(folder);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
