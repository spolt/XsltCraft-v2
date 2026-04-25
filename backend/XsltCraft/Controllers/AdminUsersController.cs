using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(IUserManagementService userMgmt) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/admin/users
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await userMgmt.ListUsersAsync(query, role, isActive, page, pageSize);
        return Ok(result);
    }

    // GET /api/admin/users/:id
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var user = await userMgmt.GetUserAsync(id);
        return user is null ? NotFound(new { message = "Kullanıcı bulunamadı." }) : Ok(user);
    }

    // POST /api/admin/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateManagedUserRequest request)
    {
        var (success, error) = await userMgmt.CreateUserAsync(request);
        if (!success) return BadRequest(new { message = error });

        return StatusCode(201, new { message = "Kullanıcı oluşturuldu." });
    }

    // PATCH /api/admin/users/:id/role
    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] SetRoleRequest request)
    {
        var (success, error) = await userMgmt.SetRoleAsync(id, CurrentUserId, request.Role);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }

    // PATCH /api/admin/users/:id/active
    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var (success, error) = await userMgmt.SetActiveAsync(id, CurrentUserId, request.IsActive);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }

    // POST /api/admin/users/:id/reset-password
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        var (success, error) = await userMgmt.ResetPasswordAsync(id, request.NewPassword);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }

    // DELETE /api/admin/users/:id
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (success, error) = await userMgmt.DeleteUserAsync(id, CurrentUserId);
        if (!success) return BadRequest(new { message = error });
        return NoContent();
    }
}
