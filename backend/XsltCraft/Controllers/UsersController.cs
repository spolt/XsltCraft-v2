using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(AppDbContext db) : ControllerBase
{
    // GET /api/users/search?query=... — paylaşım için kullanıcı arama (kendisi hariç, ilk 10)
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return Ok(Array.Empty<UserSearchResponse>());

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var term = query.Trim().ToLower();

        var users = await db.Users
            .Where(u => u.Id != userId && u.IsActive)
            .Where(u => u.Username.ToLower().Contains(term)
                     || u.Email.ToLower().Contains(term)
                     || (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)))
            .OrderBy(u => u.Username)
            .Take(10)
            .Select(u => new UserSearchResponse
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Email = u.Email
            })
            .ToListAsync();

        return Ok(users);
    }
}
