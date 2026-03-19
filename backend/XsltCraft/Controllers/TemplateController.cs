using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController(AppDbContext db, IStorageService storage) : ControllerBase
{
    // GET /api/templates  — tüm free theme'leri listele (public, auth gerektirmez)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var themes = await db.Templates
            .Where(t => t.IsFreeTheme)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new FreeThemeResponse
            {
                Id = t.Id,
                Name = t.Name,
                DocumentType = t.DocumentType.ToString(),
                ThumbnailUrl = t.ThumbnailUrl,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(themes);
    }

    // GET /api/templates/:id/download  — free theme .xslt dosyasını indir
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var template = await db.Templates.FindAsync(id);

        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        if (!template.IsFreeTheme)
            return Forbid();

        if (string.IsNullOrEmpty(template.XsltStoragePath))
            return NotFound(new { message = "Bu template için henüz XSLT dosyası yüklenmemiş." });

        if (!await storage.ExistsAsync(template.XsltStoragePath))
            return NotFound(new { message = "XSLT dosyası storage'da bulunamadı." });

        var stream = await storage.ReadAsync(template.XsltStoragePath);
        var fileName = $"{template.Name.Replace(" ", "_")}.xslt";

        return File(stream, "application/xslt+xml", fileName);
    }
}
