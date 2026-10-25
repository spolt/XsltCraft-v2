using System.Diagnostics;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Themes;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

/// <summary>
/// Hazır temaya kullanıcı logo/imza/IBAN bilgisini ekler. Önizleme (html) ve indirme/kaydetme (xslt) aynı
/// birleştirme kodundan geçer; html, dönen xslt'den <see cref="XsltRoute.Theme"/> ile render edilir (1.0 tema .NET
/// süreç içi, 2.0 tema Saxon izole worker; kullanıcı kapısı, grid girdi sınırları).
/// </summary>
[ApiController]
[Route("api/preview/compose")]
[Authorize]
[EnableRateLimiting("theme-compose")]
public class ThemeComposeController(
    AppDbContext db,
    IStorageService storage,
    IThemeAccessGuard themeAccess,
    IThemeAssetComposer composer,
    IThemeSlotProjector projector,
    IXsltExecutionService xsltExecution,
    ILogger<ThemeComposeController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 6 * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Compose([FromBody] ThemeComposeRequest request, CancellationToken ct)
    {
        if (ThemeComposeRequestValidator.Validate(request) is { } invalid)
            return BadRequest(new { error = invalid.Message, field = invalid.Field });

        var sw = Stopwatch.StartNew();
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        string xslt;
        IReadOnlyDictionary<ThemeAssetKind, string>? defaults = null;
        IReadOnlySet<string>? conditional = null;

        if (request.ThemeId is { } themeId)
        {
            var theme = await db.Templates.FirstOrDefaultAsync(t => t.Id == themeId, ct);
            if (theme is null)
                return NotFound(new { error = "Tema bulunamadı." });

            switch (await themeAccess.CheckAsync(theme, userId, ct))
            {
                case ThemeAccessDecision.NotFound:
                    return NotFound(new { error = "Tema bulunamadı." });
                case ThemeAccessDecision.UpgradeRequired:
                    return StatusCode(StatusCodes.Status402PaymentRequired,
                        new { error = "upgrade_required", upgrade = true, message = ThemeAccessGuard.UpgradeMessage });
            }

            await using var stream = await storage.ReadAsync(theme.XsltStoragePath!);
            using var reader = new StreamReader(stream);
            xslt = await reader.ReadToEndAsync(ct);
            defaults = projector.Defaults(theme.SlotCatalog);
            conditional = projector.ConditionalSlots(theme.SlotCatalog);
        }
        else
        {
            xslt = request.Xslt!;
        }

        var input = new ThemeComposeInput(
            request.Assets.Select(a => new ThemeAssetInput(ParseKind(a.Kind), a.Slot, a.DataUrl, a.Width, a.Height,
                ParseAlignment(a.Alignment))).ToList(),
            request.BankInfo.Select(b => new BankInfoInput(b.BankName.Trim(), b.Iban.Trim())).ToList(),
            defaults,
            conditional,
            request.HideThemeImages.Select(ParseKind).ToList());

        var result = composer.Compose(xslt, input);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        string? html = null;
        if (request.IncludeHtml)
        {
            XsltRunResult run;
            try
            {
                run = await xsltExecution.ExecuteAsync(
                    new XsltRunRequest(XsltRoute.Theme, result.Xslt, request.XmlContent!, XsltOperation.Render, userId), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                // Görsel verisi (data URL) loglanmaz — yalnız hata.
                logger.LogError(ex, "Tema birleştirme önizlemesi sırasında beklenmeyen hata.");
                return XsltProblemResults.Unexpected();
            }

            if (!run.Succeeded)
                return XsltProblemResults.Failed(run, Response);
            html = run.Html;
        }

        sw.Stop();
        return Ok(new ThemeComposeResponse(
            request.IncludeXslt ? result.Xslt : null,
            html,
            sw.ElapsedMilliseconds,
            result.Warnings.Select(w => new ThemeComposeWarningDto(w.Code, w.Message, KindValue(w.Kind))).ToList(),
            result.AppliedSlots.ToDictionary(kv => KindValue(kv.Key)!, kv => kv.Value)));
    }

    private static ThemeAssetKind ParseKind(string kind) => kind == "logo" ? ThemeAssetKind.Logo : ThemeAssetKind.Signature;

    private static ThemeAlignment ParseAlignment(string a) => a switch
    {
        "center" => ThemeAlignment.Center,
        "right" => ThemeAlignment.Right,
        _ => ThemeAlignment.Left,
    };

    private static string? KindValue(ThemeAssetKind? kind) => kind switch
    {
        ThemeAssetKind.Logo => "logo",
        ThemeAssetKind.Signature => "signature",
        _ => null,
    };
}
