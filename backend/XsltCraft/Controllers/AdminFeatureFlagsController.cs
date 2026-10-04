using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;
using XsltCraft.Application.Interfaces;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin/feature-flags")]
[Authorize(Roles = "Admin")]
public class AdminFeatureFlagsController : ControllerBase
{
    private readonly IAiFeatureFlagService _aiFlag;
    private readonly IAiProviderHealthService _aiHealth;
    private readonly IUsageQuotaService _quota;
    private readonly IAiVisionAvailability _vision;
    private readonly AiOptions _aiOptions;

    public AdminFeatureFlagsController(
        IAiFeatureFlagService aiFlag,
        IAiProviderHealthService aiHealth,
        IUsageQuotaService quota,
        IAiVisionAvailability vision,
        IOptions<AiOptions> aiOptions)
    {
        _aiFlag = aiFlag;
        _aiHealth = aiHealth;
        _quota = quota;
        _vision = vision;
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
        var provider = await _aiFlag.GetStringAsync(AiFlagKeys.PreferredProvider, ct)
                       ?? _aiOptions.PreferredProvider;
        return Ok(new { provider });
    }

    [HttpPut("ai/provider")]
    public async Task<IActionResult> SetAiProvider([FromBody] SetProviderRequest req, CancellationToken ct)
    {
        if (req.Provider is not ("auto" or "ollama" or "gemini"))
            return BadRequest(new { error = "invalid_provider", message = "Geçerli değerler: auto, ollama, gemini." });

        await _aiFlag.SetStringAsync(AiFlagKeys.PreferredProvider, req.Provider, ct);
        return Ok(new { provider = req.Provider });
    }

    /// <summary>
    /// Ekran görüntüsü (vision) ayarları. <c>providers</c>: görselli isteğin etkin sağlayıcı sırası
    /// (boş = kullanılamaz). Tercih "ollama" iken Gemini yalnız <c>geminiFallback</c> açıksa yedek olur.
    /// </summary>
    [HttpGet("ai/vision")]
    public async Task<IActionResult> GetAiVision(CancellationToken ct)
        => Ok(await BuildVisionStatusAsync(ct));

    [HttpPut("ai/vision")]
    public async Task<IActionResult> SetAiVision([FromBody] SetVisionRequest req, CancellationToken ct)
    {
        if (req.Enabled is { } enabled)
            await _aiFlag.SetBoolAsync(AiFlagKeys.VisionEnabled, enabled, ct);
        if (req.GeminiFallback is { } fallback)
            await _aiFlag.SetBoolAsync(AiFlagKeys.VisionGeminiFallback, fallback, ct);

        return Ok(await BuildVisionStatusAsync(ct));
    }

    private async Task<AiVisionStatusResponse> BuildVisionStatusAsync(CancellationToken ct)
    {
        var enabled = await _aiFlag.GetBoolAsync(AiFlagKeys.VisionEnabled, ct) ?? _aiOptions.Vision.Enabled;
        var geminiFallback = await _aiFlag.GetBoolAsync(AiFlagKeys.VisionGeminiFallback, ct) ?? _aiOptions.Vision.GeminiFallback;
        var providers = await _vision.ResolveProvidersAsync(ct);
        var visionModel = _aiOptions.Ollama.VisionModel;
        return new AiVisionStatusResponse(
            enabled,
            geminiFallback,
            string.IsNullOrWhiteSpace(visionModel) ? null : visionModel,
            providers);
    }

    /// <summary>Günlük token kullanımı (admin raporu).</summary>
    [HttpGet("ai/usage")]
    public async Task<IActionResult> GetAiUsage([FromQuery] string? date, CancellationToken ct)
    {
        var targetDate = date != null && DateOnly.TryParse(date, out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var users = await _quota.GetDailyUsageAsync(targetDate, ct);
        var limit = _aiOptions.DailyTokenBudgetPerUser;
        return Ok(new { date = targetDate, limit, users });
    }
}

public record SetFeatureFlagRequest(bool Enabled);
public record SetProviderRequest(string Provider);
/// <summary>Kısmi güncelleme: null alanlar değiştirilmez.</summary>
public record SetVisionRequest(bool? Enabled, bool? GeminiFallback);
public record AiVisionStatusResponse(bool Enabled, bool GeminiFallback, string? OllamaVisionModel, IReadOnlyList<string> Providers);
