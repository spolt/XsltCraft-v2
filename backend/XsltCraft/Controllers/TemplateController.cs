using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
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

    // GET /api/templates/:id  — tekil template'i getir (sahibi veya admin)
    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (!template.IsFreeTheme && template.OwnerId != userId && role != "Admin")
            return Forbid();

        return Ok(new TemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            DocumentType = template.DocumentType.ToString(),
            IsFreeTheme = template.IsFreeTheme,
            BlockTree = template.BlockTree,
            ThumbnailUrl = template.ThumbnailUrl,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // POST /api/templates  — yeni kullanıcı template'i oluştur
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!Enum.TryParse<DocumentType>(request.DocumentType, ignoreCase: true, out var docType))
            return BadRequest(new { message = "Geçersiz documentType. 'Invoice' veya 'Despatch' olmalı." });

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Yeni Şablon" : request.Name.Trim(),
            OwnerId = userId,
            DocumentType = docType,
            IsFreeTheme = false,
            BlockTree = request.BlockTree,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, new TemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            DocumentType = template.DocumentType.ToString(),
            IsFreeTheme = false,
            BlockTree = template.BlockTree,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // PUT /api/templates/:id  — template'i güncelle (ad + block tree)
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateBlockTreeRequest request)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (template.OwnerId != userId)
            return Forbid();

        if (request.Name is not null)
            template.Name = request.Name.Trim();

        if (request.BlockTree is not null)
            template.BlockTree = request.BlockTree;

        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new TemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            DocumentType = template.DocumentType.ToString(),
            IsFreeTheme = template.IsFreeTheme,
            BlockTree = template.BlockTree,
            ThumbnailUrl = template.ThumbnailUrl,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }
}
