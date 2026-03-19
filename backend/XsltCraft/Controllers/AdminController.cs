using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, IStorageService storage) : ControllerBase
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    // POST /api/admin/themes
    [HttpPost("themes")]
    public async Task<IActionResult> CreateTheme(
        [FromForm] string name,
        [FromForm] string documentType,
        IFormFile file,
        IFormFile? thumbnailFile = null)
    {
        var validationError = ValidateXsltFile(file);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            return BadRequest(new { message = "Geçersiz documentType. Geçerli değerler: Invoice, Despatch" });

        var themeId = Guid.NewGuid();
        var relativePath = $"themes/{themeId}.xslt";

        await using (var stream = file.OpenReadStream())
            await storage.WriteAsync(stream, relativePath, "application/xslt+xml");

        string? thumbnailUrl = null;
        if (thumbnailFile is not null)
        {
            var thumbPath = $"themes/thumbnails/{themeId}{Path.GetExtension(thumbnailFile.FileName)}";
            await using var thumbStream = thumbnailFile.OpenReadStream();
            await storage.WriteAsync(thumbStream, thumbPath, thumbnailFile.ContentType);
            thumbnailUrl = thumbPath;
        }

        var template = new Template
        {
            Id = themeId,
            Name = name,
            DocumentType = docType,
            IsFreeTheme = true,
            XsltStoragePath = relativePath,
            ThumbnailUrl = thumbnailUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTheme), new { id = themeId }, ToResponse(template));
    }

    // GET /api/admin/themes/:id  (helper for CreatedAtAction)
    [HttpGet("themes/{id:guid}")]
    public async Task<IActionResult> GetTheme(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        return template is null ? NotFound() : Ok(ToResponse(template));
    }

    // PUT /api/admin/themes/:id
    [HttpPut("themes/{id:guid}")]
    public async Task<IActionResult> UpdateTheme(
        Guid id,
        [FromForm] string? name,
        [FromForm] string? documentType,
        IFormFile? file = null)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound();

        if (file is not null)
        {
            var validationError = ValidateXsltFile(file);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            // Eskiyi sil, yeniyi yaz
            if (!string.IsNullOrEmpty(template.XsltStoragePath))
                await storage.DeleteAsync(template.XsltStoragePath);

            var relativePath = $"themes/{id}.xslt";
            await using var stream = file.OpenReadStream();
            await storage.WriteAsync(stream, relativePath, "application/xslt+xml");
            template.XsltStoragePath = relativePath;
        }

        if (!string.IsNullOrWhiteSpace(name))
            template.Name = name;

        if (!string.IsNullOrWhiteSpace(documentType) &&
            Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            template.DocumentType = docType;

        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToResponse(template));
    }

    // DELETE /api/admin/themes/:id
    [HttpDelete("themes/{id:guid}")]
    public async Task<IActionResult> DeleteTheme(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound();

        if (!string.IsNullOrEmpty(template.XsltStoragePath))
            await storage.DeleteAsync(template.XsltStoragePath);

        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // -------------------------------------------------------

    private static string? ValidateXsltFile(IFormFile file)
    {
        if (file.Length == 0)
            return "Dosya boş olamaz.";

        if (file.Length > MaxFileSizeBytes)
            return "Dosya boyutu 2 MB sınırını aşıyor.";

        var ext = Path.GetExtension(file.FileName);
        if (!ext.Equals(".xslt", StringComparison.OrdinalIgnoreCase))
            return "Yalnızca .xslt uzantılı dosyalar kabul edilir.";

        return null;
    }

    private static FreeThemeResponse ToResponse(Template t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        DocumentType = t.DocumentType.ToString(),
        ThumbnailUrl = t.ThumbnailUrl,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
