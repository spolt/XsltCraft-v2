using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Controllers;

/// <summary>
/// Admin: AI geri bildirim havuzunu görüntüler ve kaliteli pozitif örnekleri global havuza terfi eder.
/// Global örnekler tüm kullanıcıların prompt'una few-shot olarak girer.
/// </summary>
[ApiController]
[Route("api/admin/ai-feedback")]
[Authorize(Roles = "Admin")]
public class AdminAiFeedbackController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? rating,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var query = db.AiFeedbacks.AsNoTracking().AsQueryable();

        // Varsayılan: pozitifler (terfi adayları). "all" → filtre yok.
        if (!string.Equals(rating, "all", StringComparison.OrdinalIgnoreCase))
        {
            var wanted = string.Equals(rating, "negative", StringComparison.OrdinalIgnoreCase)
                ? AiFeedbackRating.Negative
                : AiFeedbackRating.Positive;
            query = query.Where(f => f.Rating == wanted);
        }

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => EF.Functions.ILike(f.UserMessage, $"%{q}%"));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Users, f => f.UserId, u => u.Id, (f, u) => new
            {
                f.Id,
                f.UserId,
                Username = (string?)u.Username,
                u.Email,
                Rating = f.Rating.ToString(),
                f.UserMessage,
                f.AssistantAnswer,
                f.Applied,
                f.IsGlobal,
                f.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { items, total });
    }

    /// <summary>Pozitif bir örneği global havuza ekler/çıkarır. Yalnız Positive terfi edilebilir.</summary>
    [HttpPut("{id:guid}/global")]
    public async Task<IActionResult> SetGlobal(Guid id, [FromBody] SetGlobalRequest req, CancellationToken ct)
    {
        var feedback = await db.AiFeedbacks.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (feedback is null) return NotFound();
        if (feedback.Rating != AiFeedbackRating.Positive)
            return BadRequest(new { error = "only_positive_promotable" });

        feedback.IsGlobal = req.IsGlobal;
        feedback.PromotedAt = req.IsGlobal ? DateTime.UtcNow : null;
        feedback.PromotedByUserId = req.IsGlobal ? CurrentUserId : null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var removed = await db.AiFeedbacks.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
        return removed > 0 ? NoContent() : NotFound();
    }
}

public record SetGlobalRequest(bool IsGlobal);
