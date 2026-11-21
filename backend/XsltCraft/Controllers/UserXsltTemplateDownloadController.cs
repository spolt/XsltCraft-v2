using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

using XsltCraft.Api.Downloads;
using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Templates.Download;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

/// <summary>
/// "Şablonlarım" ZIP indirme uçları (seçili şablonlar / klasörün tamamı). Yalnız sahip olunan şablonlar arşive girer;
/// paylaşılanlar ve başkasının klasörü 404. Tüm doğrulama (sahiplik, sayı, toplam boyut) arşiv üretiminden önce yapılır.
/// </summary>
[ApiController]
[Route("api/user-xslt-templates")]
[Authorize]
public sealed class UserXsltTemplateDownloadController(
    AppDbContext db,
    ITemplateArchiveWriter archiveWriter,
    IUserActivityRecorder activity,
    ArchiveDownloadGate gate) : ControllerBase
{
    private const string SelectionArchiveName = "sablonlarim.zip";

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST /api/user-xslt-templates/download — seçili şablonları ZIP olarak indir (yalnız sahip)
    [HttpPost("download")]
    [EnableRateLimiting(RateLimitPolicies.TemplateDownload)]
    public async Task<IActionResult> DownloadSelected([FromBody] DownloadUserXsltRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;

        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { message = "Şablon seçilmedi." });
        if (ids.Count > DownloadLimits.MaxDownloadItems)
            return BadRequest(new { message = $"Tek seferde en fazla {DownloadLimits.MaxDownloadItems} şablon indirilebilir." });

        var query = db.UserXsltTemplates
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id) && t.OwnerId == userId);

        if (!await query.AnyAsync(ct))
            return NotFound(new { message = "İndirilecek şablon bulunamadı." });

        if (await TotalCharsAsync(query, ct) > DownloadLimits.MaxDownloadChars)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { error = "download_too_large", message = "Seçim çok büyük, daha küçük parçalar halinde indirin." });

        return await ArchiveResultAsync(query, SelectionArchiveName, userId,
            "Seçim çok büyük, daha küçük parçalar halinde indirin.", ct);
    }

    // GET /api/user-xslt-templates/folders/:folderId/download — klasördeki tüm şablonları ZIP olarak indir (yalnız sahip)
    [HttpGet("folders/{folderId:guid}/download")]
    [EnableRateLimiting(RateLimitPolicies.TemplateDownload)]
    public async Task<IActionResult> DownloadFolder(Guid folderId, CancellationToken ct)
    {
        var userId = CurrentUserId;

        var folderName = await db.Folders
            .AsNoTracking()
            .Where(f => f.Id == folderId && f.OwnerId == userId && f.Kind == FolderKind.XsltTemplate)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(ct);
        if (folderName is null)
            return NotFound(new { message = "Klasör bulunamadı." });

        var query = db.UserXsltTemplates
            .AsNoTracking()
            .Where(t => t.FolderId == folderId && t.OwnerId == userId);

        if (!await query.AnyAsync(ct))
            return NotFound(new { error = "folder_empty", message = "Klasörde indirilecek şablon yok." });

        if (await TotalCharsAsync(query, ct) > DownloadLimits.MaxDownloadChars)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { error = "download_too_large", message = "Klasör tek seferde indirilemeyecek kadar büyük; şablonları seçerek parçalar halinde indirin." });

        return await ArchiveResultAsync(query, ArchiveFileNames.SanitizeStem(folderName) + ".zip", userId,
            "Klasör tek seferde indirilemeyecek kadar büyük; şablonları seçerek parçalar halinde indirin.", ct);
    }

    private static Task<long> TotalCharsAsync(IQueryable<UserXsltTemplate> query, CancellationToken ct) =>
        query.SumAsync(t => (long)t.XsltContent.Length, ct);

    /// <summary>
    /// ZIP'i önce geçici dosyaya yazar, sonra dosyadan asenkron sunar. .NET 10 ZIP girdi kapanışı hedefe senkron yazdığı için
    /// yanıt gövdesine doğrudan akış yapılmaz (Kestrel senkron IO'yu reddeder); bellek tamponu da kullanılmaz.
    /// Eşzamanlılık kapısı (kullanıcı başına 1, sunucu geneli sınırlı) yanıt bitene kadar tutulur; geçici dosya yanıt bitince
    /// (<see cref="FileOptions.DeleteOnClose"/>) silinir. Arşiv <see cref="DownloadLimits.MaxArchiveBytes"/>'ı aşarsa 413.
    /// </summary>
    private async Task<IActionResult> ArchiveResultAsync(
        IQueryable<UserXsltTemplate> query, string fileName, Guid userId, string tooLargeMessage, CancellationToken ct)
    {
        var denial = gate.TryEnter(userId, out var lease);
        if (denial != ArchiveDownloadGate.Denial.None)
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "download_busy",
                message = denial == ArchiveDownloadGate.Denial.UserBusy
                    ? "Devam eden bir indirmeniz var; bitince tekrar deneyin."
                    : "Sunucu şu an başka indirmeleri hazırlıyor; biraz sonra tekrar deneyin.",
            });
        Response.RegisterForDispose(lease!);

        var entries = query
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Select(t => new ArchiveEntrySource(t.Name, t.XsltContent, t.UpdatedAt))
            .AsAsyncEnumerable();

        var temp = gate.CreateTempFile();
        try
        {
            await archiveWriter.WriteAsync(entries, new LengthLimitedStream(temp, DownloadLimits.MaxArchiveBytes), ct);
            await temp.FlushAsync(ct);
            temp.Position = 0;
            await activity.RecordAsync(userId, UserActivityType.Download, null, "Xslt");
        }
        catch (ArchiveTooLargeException)
        {
            await temp.DisposeAsync();
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { error = "download_too_large", message = tooLargeMessage });
        }
        catch
        {
            await temp.DisposeAsync();
            throw;
        }

        Response.Headers[HeaderNames.CacheControl] = "no-store";
        // fileDownloadName → Content-Disposition: attachment; filename=(ASCII); filename*=UTF-8''… (SetHttpFileName)
        return File(temp, "application/zip", fileName);
    }
}
