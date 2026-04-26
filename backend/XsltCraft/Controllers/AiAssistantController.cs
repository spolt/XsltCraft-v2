using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using XsltCraft.Application.Ai;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiAssistantController : ControllerBase
{
    private readonly AiProviderOrchestrator _orchestrator;
    private readonly IAiFeatureFlagService _flag;
    private readonly IAiTokenBudgetService _tokenBudget;
    private readonly ILogger<AiAssistantController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AiAssistantController(
        AiProviderOrchestrator orchestrator,
        IAiFeatureFlagService flag,
        IAiTokenBudgetService tokenBudget,
        ILogger<AiAssistantController> logger)
    {
        _orchestrator = orchestrator;
        _flag = flag;
        _tokenBudget = tokenBudget;
        _logger = logger;
    }

    /// <summary>AI etkin mi? UI bu çağrıyla AI butonlarını gizler/gösterir.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var enabled = await _flag.IsEnabledAsync(ct);
        return Ok(new { enabled });
    }

    [HttpPost("assistant")]
    [EnableRateLimiting("ai-assistant")]
    public Task Assistant([FromBody] AssistantRequest req, CancellationToken ct)
        => StreamAsync(new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserXslt = req.Xslt,
            UserXml = req.Xml,
            XmlSelection = req.XmlSelection,
            History = req.History?.Select(h => new AssistantMessage(h.Role, h.Content)).ToList(),
            UserRequest = req.Message,
        }, ct);

    [HttpPost("refactor-selection")]
    [EnableRateLimiting("ai-assistant")]
    public Task RefactorSelection([FromBody] RefactorSelectionRequest req, CancellationToken ct)
        => StreamAsync(new AiRequest
        {
            Task = AiTaskKind.RefactorSelection,
            UserXslt = req.Xslt,
            Selection = req.Selection,
            UserRequest = req.Goal ?? "Seçimi okunabilirlik ve doğruluk açısından iyileştir.",
        }, ct);

    private async Task StreamAsync(AiRequest req, CancellationToken ct)
    {
        if (!await _flag.IsEnabledAsync(ct))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await WriteJsonAsync(new { error = "ai_disabled", message = "AI asistan yöneticisi tarafından kapatılmış." }, ct);
            return;
        }

        var userId = GetUserId();
        if (userId.HasValue && !await _tokenBudget.IsWithinBudgetAsync(userId.Value, ct))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await WriteJsonAsync(new { error = "budget_exceeded", message = "Günlük AI token bütçeniz doldu. Yarın tekrar deneyin." }, ct);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";
        Response.Headers["Cache-Control"] = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no";

        var writer = Response.BodyWriter;
        int totalOutputChars = 0;
        try
        {
            await foreach (var chunk in _orchestrator.StreamAsync(req, ct))
            {
                if (chunk.Type == "delta" && chunk.Text != null)
                    totalOutputChars += chunk.Text.Length;
                await WriteChunkAsync(writer, chunk, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnect — sessizce çık.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI streaming sırasında beklenmeyen hata.");
            try
            {
                await WriteChunkAsync(writer, new AiChunk
                {
                    Type = "error",
                    Code = "internal_error",
                    Message = "AI yanıtı üretilirken sunucu hatası oluştu.",
                }, CancellationToken.None);
            }
            catch { /* yutuldu */ }
        }
        finally
        {
            await writer.CompleteAsync();
            if (userId.HasValue && totalOutputChars > 0)
                await _tokenBudget.IncrementAsync(userId.Value, totalOutputChars / 4 + 1, CancellationToken.None);
        }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private static async Task WriteChunkAsync(System.IO.Pipelines.PipeWriter writer, AiChunk chunk, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(chunk, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await writer.WriteAsync(bytes, ct);
        await writer.FlushAsync(ct);
    }

    private async Task WriteJsonAsync(object payload, CancellationToken ct)
    {
        Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(json), ct);
        await Response.BodyWriter.FlushAsync(ct);
    }
}

public record AssistantRequest(
    string? Xslt,
    string? Xml,
    string? XmlSelection,
    List<AssistantMessageDto>? History,
    string Message
);

public record AssistantMessageDto(string Role, string Content);
public record RefactorSelectionRequest(string? Xslt, string Selection, string? Goal);
