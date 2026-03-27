using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/user-xslt-templates")]
[Authorize]
public class UserXsltTemplateController(AppDbContext db) : ControllerBase
{
    // GET /api/user-xslt-templates — kullanıcının kayıtlı XSLT şablonlarını listele
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var templates = await db.UserXsltTemplates
            .Where(t => t.OwnerId == userId)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new UserXsltTemplateResponse
            {
                Id = t.Id,
                Name = t.Name,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(templates);
    }

    // GET /api/user-xslt-templates/:id — tekil şablonu getir (içerikle birlikte)
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (template.OwnerId != userId)
            return Forbid();

        return Ok(new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // POST /api/user-xslt-templates — yeni XSLT şablonu oluştur
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserXsltRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (string.IsNullOrWhiteSpace(request.XsltContent))
            return BadRequest(new { message = "XSLT içeriği boş olamaz." });

        var template = new UserXsltTemplate
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Yeni Şablon" : request.Name.Trim(),
            OwnerId = userId,
            XsltContent = request.XsltContent,
            XmlContent = request.XmlContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.UserXsltTemplates.Add(template);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // PUT /api/user-xslt-templates/:id — şablonu güncelle
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserXsltRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (template.OwnerId != userId)
            return Forbid();

        if (request.Name is not null)
            template.Name = request.Name.Trim();

        if (request.XsltContent is not null)
            template.XsltContent = request.XsltContent;

        if (request.XmlContent is not null)
            template.XmlContent = request.XmlContent;

        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        });
    }

    // DELETE /api/user-xslt-templates/:id — şablonu sil
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (template.OwnerId != userId)
            return Forbid();

        db.UserXsltTemplates.Remove(template);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
