using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/user-snippets")]
[Authorize]
public class UserSnippetsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedScopes = ["xslt", "xpath", "html"];

    // GET /api/user-snippets
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var snippets = await db.UserSnippets
            .Where(s => s.OwnerId == userId)
            .OrderBy(s => s.Scope)
            .ThenBy(s => s.Prefix)
            .Select(s => new UserSnippetResponse
            {
                Id = s.Id,
                Prefix = s.Prefix,
                Body = s.Body,
                Description = s.Description,
                Scope = s.Scope,
                IsPublic = s.IsPublic,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync();

        return Ok(snippets);
    }

    // POST /api/user-snippets
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserSnippetRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (string.IsNullOrWhiteSpace(request.Prefix))
            return BadRequest(new { message = "Prefix boş olamaz." });

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Snippet içeriği boş olamaz." });

        var scope = request.Scope?.ToLowerInvariant() ?? "xslt";
        if (!AllowedScopes.Contains(scope))
            return BadRequest(new { message = "Geçersiz scope. Kabul edilenler: xslt, xpath, html." });

        var snippet = new UserSnippet
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Prefix = request.Prefix.Trim(),
            Body = request.Body,
            Description = request.Description?.Trim(),
            Scope = scope,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.UserSnippets.Add(snippet);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, ToResponse(snippet));
    }

    // PUT /api/user-snippets/:id
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserSnippetRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var snippet = await db.UserSnippets.FindAsync(id);
        if (snippet is null) return NotFound(new { message = "Snippet bulunamadı." });
        if (snippet.OwnerId != userId) return Forbid();

        if (request.Prefix is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest(new { message = "Prefix boş olamaz." });
            snippet.Prefix = request.Prefix.Trim();
        }

        if (request.Body is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { message = "Snippet içeriği boş olamaz." });
            snippet.Body = request.Body;
        }

        if (request.Description is not null)
            snippet.Description = request.Description.Trim();

        if (request.Scope is not null)
        {
            var scope = request.Scope.ToLowerInvariant();
            if (!AllowedScopes.Contains(scope))
                return BadRequest(new { message = "Geçersiz scope. Kabul edilenler: xslt, xpath, html." });
            snippet.Scope = scope;
        }

        if (request.IsPublic is not null)
            snippet.IsPublic = request.IsPublic.Value;

        snippet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToResponse(snippet));
    }

    // DELETE /api/user-snippets/:id
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var snippet = await db.UserSnippets.FindAsync(id);
        if (snippet is null) return NotFound(new { message = "Snippet bulunamadı." });
        if (snippet.OwnerId != userId) return Forbid();

        db.UserSnippets.Remove(snippet);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static UserSnippetResponse ToResponse(UserSnippet s) => new()
    {
        Id = s.Id,
        Prefix = s.Prefix,
        Body = s.Body,
        Description = s.Description,
        Scope = s.Scope,
        IsPublic = s.IsPublic,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
