using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiAssistantController : ControllerBase
{
    private readonly AiProviderOrchestrator _orchestrator;
    private readonly IAiFeatureFlagService _flag;
    private readonly IUsageQuotaService _quota;
    private readonly IAiExemplarService _exemplars;
    private readonly IAiFeedbackService _feedback;
    private readonly ILogger<AiAssistantController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AiAssistantController(
        AiProviderOrchestrator orchestrator,
        IAiFeatureFlagService flag,
        IUsageQuotaService quota,
        IAiExemplarService exemplars,
        IAiFeedbackService feedback,
        ILogger<AiAssistantController> logger)
    {
        _orchestrator = orchestrator;
        _flag = flag;
        _quota = quota;
        _exemplars = exemplars;
        _feedback = feedback;
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
            Selection = req.XsltSelection,
            XsltCursorLine = req.XsltCursorLine,
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

    /// <summary>
    /// AI yanıtı için geri bildirim kaydeder. Kota tüketmez (Free kullanıcı da oy verebilir).
    /// Pozitif kayıtlar sonraki sorularda örnek olarak kullanılır.
    /// </summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> Feedback([FromBody] AiFeedbackRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        if (!TryParseRating(req.Rating, out var rating))
            return BadRequest(new { error = "invalid_rating" });
        if (string.IsNullOrWhiteSpace(req.UserMessage) || string.IsNullOrWhiteSpace(req.AssistantAnswer))
            return BadRequest(new { error = "empty_content" });

        var id = await _feedback.RecordAsync(
            userId.Value,
            new AiFeedbackInput(rating, req.UserMessage, req.AssistantAnswer, req.Applied),
            ct);
        return Ok(new { id });
    }

    /// <summary>
    /// Mevcut geri bildirimi günceller (ör. "Uygula" örtük pozitif kaydını kullanıcı sonradan
    /// "işe yaramadı"ya çevirdiğinde). Sahiplik doğrulanır.
    /// </summary>
    [HttpPut("feedback/{id:guid}")]
    public async Task<IActionResult> UpdateFeedback(Guid id, [FromBody] AiFeedbackUpdateRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        if (!TryParseRating(req.Rating, out var rating))
            return BadRequest(new { error = "invalid_rating" });

        var ok = await _feedback.UpdateAsync(userId.Value, id, rating, req.Applied, ct);
        return ok ? NoContent() : NotFound();
    }

    private static bool TryParseRating(string? value, out AiFeedbackRating rating)
    {
        rating = AiFeedbackRating.Positive;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Equals("positive", StringComparison.OrdinalIgnoreCase)) { rating = AiFeedbackRating.Positive; return true; }
        if (value.Equals("negative", StringComparison.OrdinalIgnoreCase)) { rating = AiFeedbackRating.Negative; return true; }
        return false;
    }

    private async Task StreamAsync(AiRequest req, CancellationToken ct)
    {
        if (!await _flag.IsEnabledAsync(ct))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await WriteJsonAsync(new { error = "ai_disabled", message = "AI asistan yöneticisi tarafından kapatılmış." }, ct);
            return;
        }

        var userId = GetUserId();
        if (userId.HasValue)
        {
            var check = await _quota.CheckAiAllowedAsync(userId.Value, ct);
            if (!check.Allowed)
            {
                if (check.Reason == QuotaDenyReason.AiRequestLimit)
                {
                    // Free kullanıcı günlük 1 soru hakkını doldurdu → Pro'ya yönlendir.
                    Response.StatusCode = StatusCodes.Status402PaymentRequired;
                    await WriteJsonAsync(new { error = "ai_request_limit", upgrade = true, message = "Günlük 1 AI soru hakkınız doldu. Sınırsız soru ve 50.000 token/gün için XsltCraft Pro'ya geçin." }, ct);
                }
                else
                {
                    // Pro kullanıcı günlük token bütçesini doldurdu.
                    Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await WriteJsonAsync(new { error = "budget_exceeded", message = "Günlük AI token bütçeniz doldu. Yarın tekrar deneyin." }, ct);
                }
                return;
            }
        }

        // Öğrenme: kullanıcının (ve global) geçmiş pozitif örneklerinden benzer olanları
        // prompt'a enjekte et. Yalnızca assistant görevinde ve boş olmayan istekte.
        // Hata olursa exemplar servisi boş liste döner; sohbet asla kırılmaz.
        if (req.Task == AiTaskKind.Assistant && userId.HasValue && !string.IsNullOrWhiteSpace(req.UserRequest))
            req.Exemplars = [.. await _exemplars.GetExemplarsAsync(userId.Value, req.UserRequest, ct)];

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
            // Gate'i geçen her istek soru sayacını tüketir (Free 1/gün); token de yaklaşık olarak eklenir.
            if (userId.HasValue)
                await _quota.IncrementAiAsync(userId.Value, totalOutputChars > 0 ? totalOutputChars / 4 + 1 : 0, CancellationToken.None);
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
    string Message,
    string? XsltSelection = null,
    int? XsltCursorLine = null
);

public record AssistantMessageDto(string Role, string Content);
public record RefactorSelectionRequest(string? Xslt, string Selection, string? Goal);

public record AiFeedbackRequest(
    string Rating,
    string UserMessage,
    string AssistantAnswer,
    bool Applied = false
);

public record AiFeedbackUpdateRequest(string Rating, bool Applied);
