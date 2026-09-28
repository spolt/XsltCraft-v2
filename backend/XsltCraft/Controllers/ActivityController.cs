using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public class ActivityController(IUserActivityRecorder activity) : ControllerBase
{
    private static readonly HashSet<string> AllowedKinds = ["Template", "Xslt"];

    // Tarayıcıda üretilen (sunucuya uğramayan) indirmeler için sayaç kaydı.
    [HttpPost("download")]
    [EnableRateLimiting("activity")]
    public async Task<IActionResult> RecordDownload([FromBody] DownloadActivityRequest request)
    {
        if (!AllowedKinds.Contains(request.EntityKind))
            return BadRequest(new { error = "Geçersiz entityKind." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await activity.RecordAsync(userId, UserActivityType.Download, request.EntityId, request.EntityKind);
        return NoContent();
    }
}

public record DownloadActivityRequest(string EntityKind, Guid? EntityId);
