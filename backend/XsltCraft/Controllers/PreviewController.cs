using System.Diagnostics;
using System.Security.Claims;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Imaging;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Themes;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreviewController : ControllerBase
{
    private const int MaxRawBodyBytes = 10 * 1024 * 1024;   // 10 MB — XSLT + XML toplamı
    private const int MaxXsltBodyBytes = 1024 * 1024;        // 1 MB — validate-xslt için

    private readonly IXsltGeneratorService _generator;
    private readonly IStorageService _storage;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IUsageQuotaService _quota;
    private readonly IUserActivityRecorder _activity;
    private readonly IXsltHtmlRenderer _renderer;
    private readonly IThemeAccessGuard _themeAccess;
    private readonly ILogger<PreviewController> _logger;

    public PreviewController(
        IXsltGeneratorService generator,
        IStorageService storage,
        AppDbContext db,
        IWebHostEnvironment env,
        IUsageQuotaService quota,
        IUserActivityRecorder activity,
        IXsltHtmlRenderer renderer,
        IThemeAccessGuard themeAccess,
        ILogger<PreviewController> logger)
    {
        _generator = generator;
        _storage = storage;
        _db = db;
        _env = env;
        _quota = quota;
        _activity = activity;
        _renderer = renderer;
        _themeAccess = themeAccess;
        _logger = logger;
    }

    /// <summary>
    /// Block tree'den XSLT üretir, XML'e uygular, HTML döndürür.
    /// Request: { sections, blocks, xmlContent, assets }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var assetBase64 = await BuildAssetBase64Async(request.Blocks);
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

            var html = ApplyXslt(xslt, request.XmlContent);
            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            var (message, line, column) = DescribeXsltError(ex);
            return BadRequest(new { error = message, line, column });
        }
        catch (XmlException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Önizleme sırasında beklenmeyen hata.");
            return BadRequest(new { error = _env.IsDevelopment() ? ex.Message : "Sunucu hatası oluştu." });
        }
    }

    /// <summary>
    /// Free theme'i storage'dan okur, verilen XML'e uygular, HTML döndürür (galeri önizlemesi).
    /// Kullanıcı logo/imza/IBAN eklenmiş önizleme: POST /api/preview/compose.
    /// Request: { xmlContent }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost("theme/{themeId:guid}")]
    public async Task<IActionResult> PreviewTheme(Guid themeId, [FromBody] ThemePreviewRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var template = await _db.Templates
                .FirstOrDefaultAsync(t => t.Id == themeId);

            if (template is null)
                return NotFound(new { error = "Şablon bulunamadı." });

            if (string.IsNullOrEmpty(template.XsltStoragePath))
                return BadRequest(new { error = "Bu tema için XSLT dosyası tanımlı değil." });

            await using var stream = await _storage.ReadAsync(template.XsltStoragePath);
            using var reader = new StreamReader(stream);
            var xslt = await reader.ReadToEndAsync();

            var html = ApplyXslt(xslt, request.XmlContent);
            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            var (message, line, column) = DescribeXsltError(ex);
            return BadRequest(new { error = message, line, column });
        }
        catch (XmlException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Önizleme sırasında beklenmeyen hata.");
            return BadRequest(new { error = _env.IsDevelopment() ? ex.Message : "Sunucu hatası oluştu." });
        }
    }

    /// <summary>
    /// Kullanıcının kendi blok ağacı tabanlı şablonundan HTML önizleme üretir.
    /// V1 ve V2 blok ağacı formatlarını otomatik destekler.
    /// Request: { xmlContent }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost("user-template/{id:guid}"), Authorize]
    public async Task<IActionResult> PreviewUserTemplate(Guid id, [FromBody] UserTemplatePreviewRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
            var (xslt, genError) = _generator.GenerateFromJson(template.BlockTree);
            if (xslt is null) return BadRequest(new { error = genError });

            var html = ApplyXslt(xslt, req.XmlContent);
            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            var (message, line, column) = DescribeXsltError(ex);
            return BadRequest(new { error = message, line, column });
        }
        catch (XmlException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı şablon önizlemesi sırasında beklenmeyen hata.");
            return BadRequest(new { error = _env.IsDevelopment() ? ex.Message : "Sunucu hatası oluştu." });
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
    /// Ham XSLT + XML ile anlık önizleme — geliştirici mod için.
    /// Request: { xslt, xmlContent }
    /// Response: { html, generationTimeMs, timings: { parseMs, compileMs, transformMs, serializeMs } }
    /// </summary>
    [HttpPost("raw")]
    [RequestSizeLimit(MaxRawBodyBytes)]
    public IActionResult PreviewRaw([FromBody] RawPreviewRequest request)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(request.Xslt) > MaxRawBodyBytes / 2)
            return BadRequest(new { error = "XSLT içeriği çok büyük (maks. 5 MB)." });
        if (System.Text.Encoding.UTF8.GetByteCount(request.XmlContent) > MaxRawBodyBytes / 2)
            return BadRequest(new { error = "XML içeriği çok büyük (maks. 5 MB)." });

        try
        {
            var result = _renderer.Render(request.Xslt, request.XmlContent);
            return Ok(new
            {
                html = result.Html,
                generationTimeMs = result.TotalMs,
                timings = new { result.ParseMs, result.CompileMs, result.TransformMs, result.SerializeMs },
            });
        }
        catch (XsltException ex)
        {
            var (message, line, column) = DescribeXsltError(ex);
            return BadRequest(new { error = message, line, column });
        }
        catch (XmlException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Önizleme sırasında beklenmeyen hata.");
            return BadRequest(new { error = _env.IsDevelopment() ? ex.Message : "Sunucu hatası oluştu." });
        }
    }

    /// <summary>
    /// Block tree'den ham XSLT üretir — dosya indirme için (kaydedilmemiş grid şablonu yolu).
    /// Production indirme olduğundan TemplateController.Download ile aynı kotaya tabidir:
    /// Standart indiremez (402), Pro günde 3 (4.'te 429), Editör/Admin sınırsız.
    /// </summary>
    [HttpPost("xslt"), Authorize]
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

        var assetBase64 = await BuildAssetBase64Async(request.Blocks);
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
    /// XslCompiledTransform.Load derleme hatalarında dıştaki XsltException'ın mesajı her zaman
    /// jenerik "XSLT compile error."tır; asıl neden (geçersiz XPath, yanlış konumlanmış eleman vb.)
    /// InnerException zincirinde saklıdır. Zincirdeki tüm anlamlı mesajları birleştirir ve
    /// dıştaki satır/kolon 0 ise inner XsltException'dan doldurur.
    /// </summary>
    internal static (string Message, int Line, int Column) DescribeXsltError(XsltException ex)
    {
        int line = ex.LineNumber;
        int column = ex.LinePosition;

        var messages = new List<string>();
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message))
                messages.Add(cur.Message.Trim());
            if ((line == 0 || column == 0) && cur is XsltException xe && xe.LineNumber != 0)
            {
                line = xe.LineNumber;
                column = xe.LinePosition;
            }
        }

        var message = string.Join(" → ", messages.Distinct());
        return (message, line, column);
    }

    /// <summary>XSLT sözdizimini doğrular — geliştirici aracı.</summary>
    [HttpPost("validate-xslt")]
    [RequestSizeLimit(MaxXsltBodyBytes)]
    public IActionResult ValidateXslt([FromBody] ValidateXsltRequest request)
    {
        try
        {
            SecureXslt.Compile(request.Xslt);
            return Ok(new { valid = true });
        }
        catch (XsltException ex)
        {
            var (message, line, column) = DescribeXsltError(ex);
            return Ok(new { valid = false, error = message, line, column });
        }
        catch (XmlException ex)
        {
            return Ok(new { valid = false, error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XSLT doğrulama sırasında beklenmeyen hata.");
            return Ok(new { valid = false, error = _env.IsDevelopment() ? ex.Message : "Sunucu hatası oluştu.", line = 0, column = 0 });
        }
    }

    /// <summary>
    /// Image bloklarındaki assetId'leri storage'dan okuyup base64 data URI'ya çevirir.
    /// Bulunamayan veya hata veren asset'ler atlanır (URL fallback).
    /// </summary>
    private async Task<Dictionary<string, string>> BuildAssetBase64Async(Dictionary<string, BlockDto> blocks)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var assetIds = blocks.Values
            .Where(b => b.Type == "Image" && b.Config.ValueKind == System.Text.Json.JsonValueKind.Object)
            .Select(b =>
            {
                try
                {
                    var cfg = System.Text.Json.JsonSerializer.Deserialize<ImageAssetIdOnly>(
                        b.Config.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return cfg?.AssetId;
                }
                catch { return null; }
            })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        foreach (var id in assetIds)
        {
            if (!Guid.TryParse(id, out var guid)) continue;
            try
            {
                var asset = await _db.Assets.FindAsync(guid);
                if (asset is null || !await _storage.ExistsAsync(asset.FilePath)) continue;

                await using var stream = await _storage.ReadAsync(asset.FilePath);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var b64 = Convert.ToBase64String(ms.ToArray());
                result[id!] = $"data:{ImageUpload.ServeMimeType(asset.FilePath)};base64,{b64}";
            }
            catch { /* skip — GenerateImage will fall back to URL */ }
        }

        return result;
    }

    private string ApplyXslt(string xslt, string xmlContent) => _renderer.Render(xslt, xmlContent).Html;
}

public class ValidateXsltRequest
{
    public string Xslt { get; set; } = "";
}

public record UserTemplatePreviewRequest(string XmlContent);

file sealed class ImageAssetIdOnly
{
    [System.Text.Json.Serialization.JsonPropertyName("assetId")]
    public string? AssetId { get; set; }
}
