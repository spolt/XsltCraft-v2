using System.Diagnostics;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.Themes;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

/// <summary>
/// Hazır temaya kullanıcı logo/imza/IBAN bilgisini ekler. Önizleme (html) ve indirme/kaydetme (xslt) aynı
/// birleştirme kodundan geçer. Html rotası: <c>themeId</c> → <see cref="XsltRoute.Theme"/> (1.0 tema .NET süreç içi,
/// 2.0 tema Saxon izole worker; grid girdi sınırları); istekle gelen <c>xslt</c> kullanıcı kodudur →
/// <see cref="XsltRoute.Raw"/> (izole worker, ham XSLT sınırları).
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
    IThemeComposeGate composeGate,
    ILogger<ThemeComposeController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 6 * 1024 * 1024;
    private const string ComposeBusyRetryAfterSeconds = "2";

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
        DocumentType documentType;

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
            documentType = theme.DocumentType;
        }
        else
        {
            xslt = request.Xslt!;
            // Birleştirme bu metni API sürecinde iptalsiz işler; yapısal sınırlar ağaç kurulmadan önce denetlenir.
            if (ThemeComposeXsltLimits.Check(xslt) is { } overLimit)
                return BadRequest(new { error = overLimit.Message, field = overLimit.Field });

            documentType = ThemeDocumentTypeSniffer.Sniff(xslt);
            conditional = request.ConditionalSlots?.ToHashSet(StringComparer.Ordinal);
        }

        var input = new ThemeComposeInput(
            request.Assets.Select(a => new ThemeAssetInput(ParseKind(a.Kind), a.Slot, a.DataUrl, a.Width, a.Height,
                ParseAlignment(a.Alignment))).ToList(),
            request.BankInfo.Select(b => new BankInfoInput(b.BankName.Trim(), b.Iban.Trim())).ToList(),
            defaults,
            conditional,
            request.HideThemeImages.Select(ParseKind).ToList(),
            request.FixedNotes?.Select(ToFixedNote).ToList(),
            documentType,
            request.ThemeId is null ? request.StaticImages?.Select(ToStaticImage).ToList() : null,
            request.ThemeId is null ? request.FreeImages?.Select(ToFreeImage).ToList() : null,
            request.ThemeId is null ? request.DocumentBankEdits?.Select(ToDocumentBankEdit).ToList() : null);

        // İstekle gelen xslt birleştirmesi senkron ve iptalsizdir: süreç genelinde sınırlı, kuyruksuz. Hazır tema kapıya girmez.
        IDisposable? composeSlot = null;
        if (request.ThemeId is null && (composeSlot = composeGate.TryEnter()) is null)
        {
            Response.Headers.RetryAfter = ComposeBusyRetryAfterSeconds;
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Sunucu şu anda çok fazla şablon işliyor, birazdan tekrar deneyin." });
        }

        ThemeComposeResult result;
        using (composeSlot)
            result = composer.Compose(xslt, input);

        if (!result.Success)
            return result.ErrorCode is { } code ? BadRequest(new { error = result.Error, code }) : BadRequest(new { error = result.Error });

        string? html = null;
        if (request.IncludeHtml)
        {
            // Süreç içi Theme yolu yalnız sunucudaki hazır tema için; istekle gelen xslt API sürecinde çalışmamalı (ADR 008).
            var route = request.ThemeId is null ? XsltRoute.Raw : XsltRoute.Theme;
            XsltRunResult run;
            try
            {
                run = await xsltExecution.ExecuteAsync(
                    new XsltRunRequest(route, result.Xslt, request.XmlContent!, XsltOperation.Render, userId), ct);
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
            result.Warnings.Select(w => new ThemeComposeWarningDto(w.Code, w.Message, ThemeSlotProjector.KindValue(w.Kind))).ToList(),
            result.AppliedSlots.ToDictionary(kv => ThemeSlotProjector.KindValue(kv.Key)!, kv => kv.Value),
            result.FixedNotesSupported));
    }

    // Doğrulayıcıdan geçmiş değerler; ayrıştırma yalnız tür dönüşümü.
    private static FixedNoteInput ToFixedNote(ThemeComposeFixedNoteDto n)
    {
        FixedNoteStyles.TryParsePosition(n.Position, out var position);
        FixedNoteStyles.TryParseAlign(n.Align, out var align);
        return new FixedNoteInput(position, n.Text, n.Bold, n.Italic, n.Underline, n.FontSize, n.Color, n.FontFamily, align);
    }

    private static StaticImageInput ToStaticImage(StaticImageDto i) =>
        new(i.Index, i.Slot, i.Remove, i.Width, i.Height, i.Alignment is null ? null : ParseAlignment(i.Alignment));

    private static FreeImageInput ToFreeImage(ThemeComposeFreeImageDto f) =>
        new(f.Slot, f.DataUrl, f.Width, f.Height, ParseAlignment(f.Alignment));

    private static DocumentBankEditInput ToDocumentBankEdit(ThemeComposeDocumentBankEditDto e) =>
        new(e.Index, e.BankName.Trim(), e.Iban.Trim(), e.Remove);

    private static ThemeAssetKind ParseKind(string kind) => kind == "logo" ? ThemeAssetKind.Logo : ThemeAssetKind.Signature;

    private static ThemeAlignment ParseAlignment(string a) => a switch
    {
        "center" => ThemeAlignment.Center,
        "right" => ThemeAlignment.Right,
        _ => ThemeAlignment.Left,
    };
}
