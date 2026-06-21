using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using XsltCraft.Application.Interfaces;

namespace XsltCraft.Controllers;

/// <summary>
/// Geçerli kullanıcının etkin yetkileri ve günlük kullanımı. Frontend, indirme/kaydetme/AI/ücretli-tema
/// gate'lerini ve "kalan kota" rozetlerini bu uçtan besler. (Backend yine de otoritatif; bu yalnız UX içindir.)
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(IEntitlementService entitlements, IUsageQuotaService quota) : ControllerBase
{
    [HttpGet("entitlements")]
    public async Task<IActionResult> GetEntitlements(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ent = await entitlements.GetAsync(userId, ct);
        var usage = await quota.GetUsageAsync(userId, ct);

        return Ok(new EntitlementsResponse(
            ent.Role.ToString(),
            ent.EffectivePlan.ToString(),
            ent.IsPrivileged,
            ent.CanDownloadGridXslt,
            ent.CanSaveRawXslt,
            ent.CanAccessRawXslt,
            ent.CanUsePremiumThemes,
            ent.DailyAiRequestLimit,
            ent.DailyAiTokenLimit,
            ent.DailyTemplateExportLimit,
            new DailyUsageResponse(usage.AiRequestCount, usage.AiTokensUsed, usage.TemplateExportCount)));
    }
}

/// <summary>Limitlerde <c>0 = sınırsız</c>. Kalan kotayı frontend (limit - used) ile hesaplar.</summary>
public record EntitlementsResponse(
    string Role,
    string Plan,
    bool IsPrivileged,
    bool CanDownloadGridXslt,
    bool CanSaveRawXslt,
    bool CanAccessRawXslt,
    bool CanUsePremiumThemes,
    int DailyAiRequestLimit,
    int DailyAiTokenLimit,
    int DailyTemplateExportLimit,
    DailyUsageResponse Usage);

public record DailyUsageResponse(int AiRequestCount, int AiTokensUsed, int TemplateExportCount);
