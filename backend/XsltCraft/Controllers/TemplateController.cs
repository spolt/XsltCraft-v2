using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Preview;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController(AppDbContext db, IStorageService storage, IXsltGeneratorService generator) : ControllerBase
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

    // GET /api/templates/:id/download
    // Free theme → admin-yüklü .xslt dosyasını oku (public)
    // Kullanıcı template'i → owner kontrolü, block tree'den XSLT üret, storage'a yaz, cache
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        var fileName = $"{template.Name.Replace(" ", "_")}.xslt";

        // ── Free theme: admin-yüklü XSLT'yi oku ──────────────────────────────
        if (template.IsFreeTheme)
        {
            if (string.IsNullOrEmpty(template.XsltStoragePath))
                return NotFound(new { message = "Bu tema için henüz XSLT dosyası yüklenmemiş." });

            if (!await storage.ExistsAsync(template.XsltStoragePath))
                return NotFound(new { message = "XSLT dosyası storage'da bulunamadı." });

            var freeStream = await storage.ReadAsync(template.XsltStoragePath);
            return File(freeStream, "application/xslt+xml", fileName);
        }

        // ── Kullanıcı template'i: auth + owner kontrolü ──────────────────────
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (template.OwnerId != userId)
            return Forbid();

        // Yeniden indirme: daha önce üretildiyse storage'dan oku
        if (!string.IsNullOrEmpty(template.XsltStoragePath)
            && await storage.ExistsAsync(template.XsltStoragePath))
        {
            var cachedStream = await storage.ReadAsync(template.XsltStoragePath);
            return File(cachedStream, "application/xslt+xml", fileName);
        }

        // Block tree yoksa üretecek bir şey yok
        if (string.IsNullOrEmpty(template.BlockTree))
            return BadRequest(new { message = "Bu şablonda henüz block eklenmemiş." });

        // Block tree → XSLT üret
        BlockTreeDto tree;
        try
        {
            tree = JsonSerializer.Deserialize<BlockTreeDto>(template.BlockTree)
                   ?? throw new InvalidOperationException("Boş ağaç.");
        }
        catch
        {
            return BadRequest(new { message = "Block tree okunamadı." });
        }

        var (xslt, genError) = generator.Generate(tree);
        if (xslt is null)
            return BadRequest(new { message = genError ?? "XSLT üretilemedi." });

        // Storage'a yaz
        var storagePath = $"templates/{template.Id}.xslt";
        await using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xslt)))
            await storage.WriteAsync(ms, storagePath, "application/xslt+xml");

        // DB'de path'i güncelle
        template.XsltStoragePath = storagePath;
        await db.SaveChangesAsync();

        // Dosyayı stream et
        var resultStream = await storage.ReadAsync(storagePath);
        return File(resultStream, "application/xslt+xml", fileName);
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
            XsltStoragePath = template.XsltStoragePath,
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
        {
            template.BlockTree = request.BlockTree;
            // Block tree değişti → eski XSLT cache'i geçersiz
            if (!string.IsNullOrEmpty(template.XsltStoragePath))
            {
                if (await storage.ExistsAsync(template.XsltStoragePath))
                    await storage.DeleteAsync(template.XsltStoragePath);
                template.XsltStoragePath = null;
            }
        }

        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new TemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            DocumentType = template.DocumentType.ToString(),
            IsFreeTheme = template.IsFreeTheme,
            BlockTree = template.BlockTree,
            XsltStoragePath = template.XsltStoragePath,
            ThumbnailUrl = template.ThumbnailUrl,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // GET /api/templates/my  — kullanıcının kendi template'lerini listele
    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var templates = await db.Templates
            .Where(t => t.OwnerId == userId && !t.IsFreeTheme)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TemplateDetailResponse
            {
                Id = t.Id,
                Name = t.Name,
                DocumentType = t.DocumentType.ToString(),
                IsFreeTheme = t.IsFreeTheme,
                ThumbnailUrl = t.ThumbnailUrl,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(templates);
    }

    // POST /api/templates/:id/clone  — free theme'i veya kendi template'ini klonla
    [Authorize]
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!template.IsFreeTheme && template.OwnerId != userId)
            return Forbid();

        var clone = new Template
        {
            Id = Guid.NewGuid(),
            Name = $"{template.Name} (kopya)",
            OwnerId = userId,
            DocumentType = template.DocumentType,
            IsFreeTheme = false,
            BlockTree = template.BlockTree,
            // Free theme'in XSLT'sini referans al — preview için kullanılır
            XsltStoragePath = template.IsFreeTheme ? template.XsltStoragePath : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(clone);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = clone.Id }, new TemplateDetailResponse
        {
            Id = clone.Id,
            Name = clone.Name,
            DocumentType = clone.DocumentType.ToString(),
            IsFreeTheme = false,
            BlockTree = clone.BlockTree,
            XsltStoragePath = clone.XsltStoragePath,
            CreatedAt = clone.CreatedAt,
            UpdatedAt = clone.UpdatedAt
        });
    }

    // DELETE /api/templates/:id  — sahibi veya admin silebilir
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Template bulunamadı." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (template.OwnerId != userId && role != "Admin")
            return Forbid();

        if (!string.IsNullOrEmpty(template.XsltStoragePath) && await storage.ExistsAsync(template.XsltStoragePath))
            await storage.DeleteAsync(template.XsltStoragePath);

        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
