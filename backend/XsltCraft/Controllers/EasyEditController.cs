using System.Diagnostics;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Api.Controllers;

/// <summary>
/// Kolay düzenle: kullanıcının yüklediği XSLT'yi logo/imza/not eklemeye uygunluk için analiz eder (Pro).
/// Kullanıcı kodu olduğu için yürütme <see cref="Application.Xslt.Engines.XsltRoute.Raw"/> (izole worker).
/// Rejected ve Unavailable de 200 yanıtında birer karar durumudur.
/// </summary>
[ApiController]
[Route("api/easy-edit")]
[Authorize]
public class EasyEditController(
    IUploadedXsltAnalyzer analyzer,
    IThemeSlotProjector projector,
    IEntitlementService entitlements,
    IEasyEditAnalyzeGate gate,
    ILogger<EasyEditController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 6 * 1024 * 1024;
    private const string BusyRetryAfterSeconds = "2";
    private const string UpgradeMessage = "Kolay düzenle XsltCraft Pro üyeliği gerektirir.";

    [HttpPost("analyze")]
    [EnableRateLimiting(RateLimitPolicies.EasyEditAnalyze)]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Analyze([FromBody] EasyEditAnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Xslt))
            return BadRequest(new { error = "XSLT içeriği gerekli.", field = "xslt" });
        var bytes = Encoding.UTF8.GetByteCount(request.Xslt);
        if (bytes > ThemeComposeRequestValidator.MaxXsltBytes)
            return BadRequest(new { error = "XSLT içeriği çok büyük (maks. 4 MB).", field = "xslt" });

        List<ThemeImageRole>? roles = null;
        if (request.ImageRoles is { } requested)
        {
            if (requested.Count > UploadedXsltAnalyzer.MaxImages)
                return BadRequest(new { error = $"En fazla {UploadedXsltAnalyzer.MaxImages} görsel rolü gönderilebilir.", field = "imageRoles" });
            roles = new List<ThemeImageRole>(requested.Count);
            foreach (var value in requested)
            {
                if (!ThemeSlotProjector.TryParseRole(value, out var role))
                    return BadRequest(new { error = "Görsel rolü logo, signature ya da design olmalı.", field = "imageRoles" });
                roles.Add(role);
            }
        }

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if ((await entitlements.GetAsync(userId, ct)).EffectivePlan != MembershipPlan.Pro)
            return StatusCode(StatusCodes.Status402PaymentRequired,
                new { error = "upgrade_required", upgrade = true, message = UpgradeMessage });

        // Analiz metni API sürecinde ağaç olarak kurar; yapısal sınırlar önce denetlenir.
        if (ThemeComposeXsltLimits.Check(request.Xslt) is { } overLimit)
            return BadRequest(new { error = overLimit.Message, field = overLimit.Field });

        using var slot = gate.TryEnter();
        if (slot is null)
            return Busy();

        var sw = Stopwatch.StartNew();
        EasyEditAnalysis analysis;
        try
        {
            analysis = await analyzer.AnalyzeAsync(request.Xslt, roles, userId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            // Dosya içeriği ve görsel verisi loglanmaz.
            logger.LogError(ex, "Kolay düzenle analizi sırasında beklenmeyen hata ({Bytes} bayt).", bytes);
            return XsltProblemResults.Unexpected();
        }

        sw.Stop();
        if (analysis.Verdict == EasyEditVerdict.Busy)
            return Busy();
        logger.LogInformation("Kolay düzenle analizi: {Verdict}, {Bytes} bayt, {ElapsedMs} ms.", analysis.Verdict, bytes, sw.ElapsedMilliseconds);
        return Ok(EasyEditResponseMapper.ToResponse(analysis, projector));
    }

    private ObjectResult Busy()
    {
        Response.Headers.RetryAfter = BusyRetryAfterSeconds;
        return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Sunucu şu anda çok fazla dosya analiz ediyor, birazdan tekrar deneyin." });
    }
}
