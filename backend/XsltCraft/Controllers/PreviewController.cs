using System.Diagnostics;
using System.Security.Claims;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using XsltCraft.Api.Preview;
using XsltCraft.Api.Xslt;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Themes;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreviewController : ControllerBase
{
    // Gövde sınırları: içerik sınırları XsltExecutionService'tedir (Raw XSLT 5 MB + XML 5 MB, Validate 1 MB);
    // gövde sınırı JSON kaçışı (\" \n \uXXXX) için pay bırakır.
    private const int MaxRawBodyBytes = 12 * 1024 * 1024;    // 12 MB — raw + user-template
    private const int MaxGridBodyBytes = 10 * 1024 * 1024;   // 10 MB — grid blok ağacı + XML
    private const int MaxXsltBodyBytes = 1024 * 1024;         // 1 MB — validate-xslt
    private const int MaxThemeBodyBytes = 6 * 1024 * 1024;    // 6 MB — tema önizleme (XML 5 MB + JSON kaçış payı)

    private readonly IXsltGeneratorService _generator;
    private readonly IStorageService _storage;
    private readonly AppDbContext _db;
    private readonly IUsageQuotaService _quota;
    private readonly IUserActivityRecorder _activity;
    private readonly IXsltExecutionService _xslt;
    private readonly PreviewAssetEmbedder _assets;
    private readonly IThemeAccessGuard _themeAccess;
    private readonly ILogger<PreviewController> _logger;

    public PreviewController(
        IXsltGeneratorService generator,
        IStorageService storage,
        AppDbContext db,
        IUsageQuotaService quota,
        IUserActivityRecorder activity,
        IXsltExecutionService xslt,
        PreviewAssetEmbedder assets,
        IThemeAccessGuard themeAccess,
        ILogger<PreviewController> logger)
    {
        _generator = generator;
        _storage = storage;
        _db = db;
        _quota = quota;
        _activity = activity;
        _xslt = xslt;
        _assets = assets;
        _themeAccess = themeAccess;
        _logger = logger;
    }

    /// <summary>
    /// Block tree'den XSLT üretir, XML'e uygular, HTML döndürür (grid canlı önizleme → <see cref="XsltRoute.Grid"/>).
    /// Request: { sections, blocks, xmlContent, assets }
    /// Response: { html, generationTimeMs, timings, engine, cacheHit } · hata { error, line, column, kind, engine }
    /// </summary>
    [HttpPost, Authorize]
    [EnableRateLimiting(RateLimitPolicies.XsltPreview)]
    [RequestSizeLimit(MaxGridBodyBytes)]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest request)
    {
        var ct = HttpContext.RequestAborted;
        var sw = Stopwatch.StartNew();
        try
        {
            var userId = CurrentUserId();
            var assetBase64 = await _assets.BuildAsync(request.Blocks, userId, ct);
            string? xslt;
            string? genError;

            if (request.Version == 2)
            {
                var treeV2 = new BlockTreeV2Dto { Blocks = request.Blocks, DocumentType = request.DocumentType };
                (xslt, genError) = _generator.GenerateV2(treeV2, assetBase64);
            }
            else
            {
                var tree = new BlockTreeDto { Sections = request.Sections, Blocks = request.Blocks };
                (xslt, genError) = _generator.Generate(tree, assetBase64);
            }

            if (xslt is null)
                return BadRequest(new { error = genError });

            var warnings = _generator.Warnings.ToArray();
            var result = await _xslt.ExecuteAsync(
                new XsltRunRequest(XsltRoute.Grid, xslt, request.XmlContent, XsltOperation.Render, userId), ct);
            return XsltProblemResults.Render(result, sw.ElapsedMilliseconds, Response, warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Önizleme sırasında beklenmeyen hata.");
            return XsltProblemResults.Unexpected();
        }
    }

    /// <summary>
    /// Hazır temayı storage'dan okur, verilen XML'e uygular, HTML döndürür (galeri önizlemesi; anonim olabilir).
    /// Kullanıcı logo/imza/IBAN eklenmiş önizleme: POST /api/preview/compose.
    /// <para>G3: yalnız <see cref="IThemeAccessGuard"/>'ın izin verdiği yayınlanmış tema → <see cref="XsltRoute.Theme"/>
    /// (süreç içi .NET, grid girdi sınırları, kullanıcı kapısı — anonimde IP anahtarı). İstisna: oturum sahibinin KENDİ
    /// saklı XSLT'li şablonu (editörde tema klonu önizlemesi) → <see cref="XsltRoute.UserTemplate"/> (izole worker).
    /// Başkasının şablonu / yayınlanmamış tema → 404.</para>
    /// Request: { xmlContent } · Response: { html, generationTimeMs, timings, engine, cacheHit, warnings }
    /// </summary>
    [HttpPost("theme/{themeId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.ThemePreview)]
    [RequestSizeLimit(MaxThemeBodyBytes)]
    public async Task<IActionResult> PreviewTheme(Guid themeId, [FromBody] ThemePreviewRequest request)
    {
        var ct = HttpContext.RequestAborted;
        var sw = Stopwatch.StartNew();
        try
        {
            var template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == themeId, ct);
            if (template is null)
                return NotFound(new { error = "Şablon bulunamadı." });

            Guid? userId = User.Identity?.IsAuthenticated == true ? CurrentUserId() : null;
            XsltRoute route;
            if (userId is { } owner && template.OwnerId == owner && !template.IsFreeTheme
                && !string.IsNullOrEmpty(template.XsltStoragePath))
            {
                route = XsltRoute.UserTemplate;
            }
            else
            {
                // Ücretli tema galeride herkese ÖNİZLENİR (yalnız HTML; ham XSLT xslt-content'te Pro ister) —
                // UpgradeRequired burada engel değildir, NotFound (yayınlanmamış / kullanıcı şablonu) engeldir.
                if (await _themeAccess.CheckAsync(template, userId, ct) == ThemeAccessDecision.NotFound)
                    return NotFound(new { error = "Şablon bulunamadı." });
                route = XsltRoute.Theme;
            }

            string xslt;
            if (route == XsltRoute.UserTemplate && GeneratedXsltCache.IsStale(template.XsltStoragePath, template.Id)
                && !string.IsNullOrWhiteSpace(template.BlockTree))
            {
                // Eski sürüm önbellek asset URL'li olabilir — okunmaz; görseller gömülü olarak yeniden üretilir.
                var assetBase64 = await _assets.BuildFromJsonAsync(template.BlockTree, userId!.Value, ct);
                var (generated, genError) = _generator.GenerateFromJson(template.BlockTree, assetBase64);
                if (generated is null)
                    return BadRequest(new { error = genError });
                xslt = generated;
            }
            else
            {
                await using var stream = await _storage.ReadAsync(template.XsltStoragePath!);
                using var reader = new StreamReader(stream);
                xslt = await reader.ReadToEndAsync(ct);
            }

            var result = await _xslt.ExecuteAsync(
                new XsltRunRequest(route, xslt, request.XmlContent ?? "", XsltOperation.Render, userId ?? AnonymousGateKey()), ct);

            // XML ayrıştırma hatasında motorun ham XmlException metni yerine sabit mesaj (konum korunur).
            if (result.Diagnostic is { Kind: XsltFailureKind.Xml } xmlError)
                result = result with { Diagnostic = xmlError with { Message = "XML içeriği okunamadı (geçerli bir XML değil)." } };

            return XsltProblemResults.Render(result, sw.ElapsedMilliseconds, Response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tema önizlemesi sırasında beklenmeyen hata.");
            return XsltProblemResults.Unexpected();
        }
    }

    /// <summary>Anonim kullanıcı kapısı bölümü: istemci IP'sinden türetilmiş kararlı anahtar (IP'nin kendisi değil).</summary>
    private Guid AnonymousGateKey()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("theme-preview:" + ip));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Kullanıcının kendi blok ağacı tabanlı şablonundan HTML önizleme üretir.
    /// V1 ve V2 blok ağacı formatlarını otomatik destekler.
    /// Request: { xmlContent }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost("user-template/{id:guid}"), Authorize]
    [EnableRateLimiting(RateLimitPolicies.XsltPreview)]
    [RequestSizeLimit(MaxRawBodyBytes)]
    public async Task<IActionResult> PreviewUserTemplate(Guid id, [FromBody] UserTemplatePreviewRequest req)
    {
        var ct = HttpContext.RequestAborted;
        var userId = CurrentUserId();
        var template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
        if (template is null) return NotFound(new { error = "Şablon bulunamadı." });

        const string noContentHtml =
            "<html><body style='display:flex;align-items:center;justify-content:center;height:100%;" +
            "color:#6b7280;font-family:sans-serif;font-size:13px'>" +
            "Bu şablona henüz içerik eklenmemiş. Editörde blok ekledikten sonra önizleyin." +
            "</body></html>";

        if (string.IsNullOrWhiteSpace(template.BlockTree))
            return Ok(new { html = noContentHtml, generationTimeMs = 0 });

        // Blok ağacı boş olsa bile JSON geçerli olabilir ({"version":2,"blocks":{}}) —
        // bu durumda XSLT boş HTML üretir. Kullanıcıya bilgilendirici mesaj göster.
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(template.BlockTree);
            var root = doc.RootElement;
            bool hasBlocks = false;
            if (root.TryGetProperty("blocks", out var blocksEl) &&
                blocksEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                hasBlocks = blocksEl.EnumerateObject().Any();
            else if (root.TryGetProperty("sections", out var sectionsEl) &&
                     sectionsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                hasBlocks = sectionsEl.GetArrayLength() > 0;

            if (!hasBlocks)
                return Ok(new { html = noContentHtml, generationTimeMs = 0 });
        }
        catch { /* JSON parse hatası → XSLT generator'a bırak */ }

        var sw = Stopwatch.StartNew();
        try
        {
            var assetBase64 = await _assets.BuildFromJsonAsync(template.BlockTree, userId, ct);
            var (xslt, genError) = _generator.GenerateFromJson(template.BlockTree, assetBase64);
            if (xslt is null) return BadRequest(new { error = genError });

            var warnings = _generator.Warnings.ToArray();
            var result = await _xslt.ExecuteAsync(
                new XsltRunRequest(XsltRoute.UserTemplate, xslt, req.XmlContent, XsltOperation.Render, userId), ct);
            return XsltProblemResults.Render(result, sw.ElapsedMilliseconds, Response, warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı şablon önizlemesi sırasında beklenmeyen hata.");
            return XsltProblemResults.Unexpected();
        }
    }

    /// <summary>
    /// Free theme XSLT içeriğini döndürür — geliştirici mod için.
    /// Response: raw XSLT text (application/xslt+xml)
    /// </summary>
    [HttpGet("theme/{themeId:guid}/xslt-content")]
    public async Task<IActionResult> GetThemeXsltContent(Guid themeId)
    {
        var template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == themeId);
        if (template is null)
            return NotFound(new { error = "Şablon bulunamadı." });

        // Yalnız yayınlanmış hazır temalar (kullanıcı şablonlarının saklı XSLT'si bu yoldan okunamaz);
        // ücretli temanın ham XSLT'si (geliştirici modu) yalnız Pro/Editör/Admin'e açıktır.
        Guid? userId = User.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        switch (await _themeAccess.CheckAsync(template, userId))
        {
            case ThemeAccessDecision.NotFound:
                return NotFound(new { error = "Şablon bulunamadı." });
            case ThemeAccessDecision.UpgradeRequired:
                return StatusCode(StatusCodes.Status402PaymentRequired,
                    new { error = "upgrade_required", upgrade = true, message = ThemeAccessGuard.UpgradeMessage });
        }

        await using var stream = await _storage.ReadAsync(template.XsltStoragePath!);
        using var reader = new StreamReader(stream);
        var xslt = await reader.ReadToEndAsync();
        return Content(xslt, "application/xslt+xml");
    }

    /// <summary>
    /// Ham XSLT + XML ile anlık önizleme — geliştirici mod / XSLT editörü (<see cref="XsltRoute.Raw"/>: 1.0 → .NET,
    /// 2.0 → Saxon, izole worker). İçerik sınırları serviste (XSLT 5 MB + XML 5 MB); gövde sınırı JSON kaçış payıyla 12 MB.
    /// Request: { xslt, xmlContent }
    /// Response: { html, generationTimeMs, timings: { parseMs, compileMs, transformMs, serializeMs }, engine, cacheHit }
    /// </summary>
    [HttpPost("raw"), Authorize]
    [EnableRateLimiting(RateLimitPolicies.XsltPreview)]
    [RequestSizeLimit(MaxRawBodyBytes)]
    public async Task<IActionResult> PreviewRaw([FromBody] RawPreviewRequest request)
    {
        var ct = HttpContext.RequestAborted;
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _xslt.ExecuteAsync(
                new XsltRunRequest(XsltRoute.Raw, request.Xslt ?? "", request.XmlContent ?? "", XsltOperation.Render, CurrentUserId()), ct);
            return XsltProblemResults.Render(result, sw.ElapsedMilliseconds, Response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Önizleme sırasında beklenmeyen hata.");
            return XsltProblemResults.Unexpected();
        }
    }

    /// <summary>
    /// Block tree'den ham XSLT üretir — dosya indirme için (kaydedilmemiş grid şablonu yolu).
    /// Production indirme olduğundan TemplateController.Download ile aynı kotaya tabidir:
    /// Standart indiremez (402), Pro günde 3 (4.'te 429), Editör/Admin sınırsız.
    /// </summary>
    [HttpPost("xslt"), Authorize]
    [EnableRateLimiting(RateLimitPolicies.XsltPreview)]
    [RequestSizeLimit(MaxGridBodyBytes)]
    public async Task<IActionResult> GenerateXslt([FromBody] PreviewRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var check = await _quota.CheckTemplateExportAsync(userId);
        if (!check.Allowed)
            return check.Reason == QuotaDenyReason.ExportDailyLimit
                ? StatusCode(StatusCodes.Status429TooManyRequests,
                    new { error = "daily_export_limit", message = "Bugünkü 3 şablon indirme hakkınız doldu. Yarın tekrar deneyebilir veya ek paket alabilirsiniz." })
                : StatusCode(StatusCodes.Status402PaymentRequired,
                    new { error = "upgrade_required", upgrade = true, message = "Şablon indirme XsltCraft Pro üyeliği gerektirir." });

        var assetBase64 = await _assets.BuildAsync(request.Blocks, userId, HttpContext.RequestAborted);
        string? xslt;
        string? error;

        if (request.Version == 2)
        {
            var treeV2 = new BlockTreeV2Dto { Blocks = request.Blocks, DocumentType = request.DocumentType };
            (xslt, error) = _generator.GenerateV2(treeV2, assetBase64);
        }
        else
        {
            var tree = new BlockTreeDto { Sections = request.Sections, Blocks = request.Blocks };
            (xslt, error) = _generator.Generate(tree, assetBase64);
        }

        if (xslt is null)
            return BadRequest(new { error });

        await _quota.IncrementTemplateExportAsync(userId);
        await _activity.RecordAsync(userId, UserActivityType.Download, null, "Template");
        return Content(xslt, "application/xslt+xml");
    }

    /// <summary>
    /// XsltException zincirini tek mesaj + satır/kolona indirger — mantık
    /// <see cref="XsltCraft.Application.Xslt.Engines.XsltDiagnostics.FromXsltException"/>'dadır.
    /// </summary>
    internal static (string Message, int Line, int Column) DescribeXsltError(XsltException ex)
    {
        var d = XsltCraft.Application.Xslt.Engines.XsltDiagnostics.FromXsltException(ex);
        return (d.Message, d.Line, d.Column);
    }

    /// <summary>
    /// XSLT'yi derleyerek doğrular — geliştirici aracı (<see cref="XsltRoute.Raw"/> + <see cref="XsltOperation.Validate"/>).
    /// İçerik hatası → <c>200 { valid:false, error, line, column, kind, engine }</c>; operasyonel hata 429/503/500.
    /// </summary>
    [HttpPost("validate-xslt"), Authorize]
    [EnableRateLimiting(RateLimitPolicies.XsltValidate)]
    [RequestSizeLimit(MaxXsltBodyBytes)]
    public async Task<IActionResult> ValidateXslt([FromBody] ValidateXsltRequest request)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            var result = await _xslt.ExecuteAsync(
                new XsltRunRequest(XsltRoute.Raw, request.Xslt ?? "", null, XsltOperation.Validate, CurrentUserId()), ct);
            return XsltProblemResults.Validate(result, Response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XSLT doğrulama sırasında beklenmeyen hata.");
            return XsltProblemResults.Unexpected();
        }
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public class ValidateXsltRequest
{
    public string Xslt { get; set; } = "";
}

public record UserTemplatePreviewRequest(string XmlContent);
