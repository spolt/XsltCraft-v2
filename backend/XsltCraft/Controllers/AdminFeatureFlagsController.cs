using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin/feature-flags")]
[Authorize(Roles = "Admin")]
public class AdminFeatureFlagsController : ControllerBase
{
    private readonly IAiFeatureFlagService _aiFlag;
    private readonly IAiProviderHealthService _aiHealth;
    private readonly IAiTokenBudgetService _tokenBudget;
    private readonly AiOptions _aiOptions;

    public AdminFeatureFlagsController(
        IAiFeatureFlagService aiFlag,
        IAiProviderHealthService aiHealth,
        IAiTokenBudgetService tokenBudget,
        IOptions<AiOptions> aiOptions)
    {
        _aiFlag = aiFlag;
        _aiHealth = aiHealth;
        _tokenBudget = tokenBudget;
        _aiOptions = aiOptions.Value;
    }

    [HttpGet("ai")]
    public async Task<IActionResult> GetAi(CancellationToken ct)
    {
        var enabled = await _aiFlag.IsEnabledAsync(ct);
        return Ok(new { enabled });
    }

    [HttpPut("ai")]
    public async Task<IActionResult> SetAi([FromBody] SetFeatureFlagRequest req, CancellationToken ct)
    {
        await _aiFlag.SetEnabledAsync(req.Enabled, ct);
        return Ok(new { enabled = req.Enabled });
    }

    /// <summary>Admin paneli için AI sağlayıcı sağlık raporu.</summary>
    [HttpGet("ai/health")]
    public async Task<IActionResult> GetAiHealth(CancellationToken ct)
    {
        var providers = await _aiHealth.CheckAsync(ct);
        return Ok(new { providers });
    }

    /// <summary>Etkin sağlayıcı tercihi: "auto" | "ollama" | "gemini".</summary>
    [HttpGet("ai/provider")]
    public async Task<IActionResult> GetAiProvider(CancellationToken ct)
    {
        var provider = await _aiFlag.GetStringAsync("ai.preferred_provider", ct)
                       ?? _aiOptions.PreferredProvider;
        return Ok(new { provider });
    }

    [HttpPut("ai/provider")]
    public async Task<IActionResult> SetAiProvider([FromBody] SetProviderRequest req, CancellationToken ct)
    {
        if (req.Provider is not ("auto" or "ollama" or "gemini"))
            return BadRequest(new { error = "invalid_provider", message = "Geçerli değerler: auto, ollama, gemini." });

        await _aiFlag.SetStringAsync("ai.preferred_provider", req.Provider, ct);
        return Ok(new { provider = req.Provider });
    }

    /// <summary>Günlük token kullanımı (admin raporu).</summary>
    [HttpGet("ai/usage")]
    public async Task<IActionResult> GetAiUsage([FromQuery] string? date, CancellationToken ct)
    {
        var targetDate = date != null && DateOnly.TryParse(date, out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var users = await _tokenBudget.GetDailyUsageAsync(targetDate, ct);
        var limit = _aiOptions.DailyTokenBudgetPerUser;
        return Ok(new { date = targetDate, limit, users });
    }
}

public record SetFeatureFlagRequest(bool Enabled);
public record SetProviderRequest(string Provider);
