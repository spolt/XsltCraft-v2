using System.Diagnostics;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Preview;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreviewController : ControllerBase
{
    private const int MaxRawBodyBytes = 1 * 1024 * 1024;   // 1 MB — XSLT + XML toplamı
    private const int MaxXsltBodyBytes = 512 * 1024;        // 512 KB — validate-xslt için

    private readonly IXsltGeneratorService _generator;
    private readonly IStorageService _storage;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PreviewController> _logger;

    public PreviewController(
        IXsltGeneratorService generator,
        IStorageService storage,
        AppDbContext db,
        IWebHostEnvironment env,
        ILogger<PreviewController> logger)
    {
        _generator = generator;
        _storage = storage;
        _db = db;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Block tree'den XSLT üretir, XML'e uygular, HTML döndürür.
    /// Request: { sections, blocks, xmlContent, assets }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost]
    public IActionResult Preview([FromBody] PreviewRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tree = new BlockTreeDto
            {
                Sections = request.Sections,
                Blocks = request.Blocks,
            };

            var (xslt, genError) = _generator.Generate(tree);
            if (xslt is null)
                return BadRequest(new { error = genError });

            var html = ApplyXslt(xslt, request.XmlContent);
            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
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
    /// Free theme'i storage'dan okur, verilen XML'e uygular, HTML döndürür.
    /// Request: { xmlContent, logoUrl?, signatureUrl?, bankInfo? }
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

            var html = ApplyXsltWithParams(xslt, request.XmlContent, request.LogoUrl, request.SignatureUrl);

            // Override logo/signature divs populated by the XSLT's JavaScript
            if (!string.IsNullOrEmpty(request.LogoUrl) || !string.IsNullOrEmpty(request.SignatureUrl))
                html = InjectAssetOverrides(html,
                    request.LogoUrl, request.LogoWidth, request.LogoHeight, request.LogoAlignment,
                    request.SignatureUrl, request.SignatureWidth, request.SignatureHeight, request.SignatureAlignment);

            if (request.BankInfo?.Count > 0)
                html = InjectBankInfo(html, request.BankInfo);

            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
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
    /// Free theme XSLT içeriğini döndürür — geliştirici mod için.
    /// Response: raw XSLT text (application/xslt+xml)
    /// </summary>
    [HttpGet("theme/{themeId:guid}/xslt-content")]
    public async Task<IActionResult> GetThemeXsltContent(Guid themeId)
    {
        var template = await _db.Templates.FirstOrDefaultAsync(t => t.Id == themeId);
        if (template is null)
            return NotFound(new { error = "Şablon bulunamadı." });
        if (string.IsNullOrEmpty(template.XsltStoragePath))
            return BadRequest(new { error = "Bu tema için XSLT dosyası tanımlı değil." });

        await using var stream = await _storage.ReadAsync(template.XsltStoragePath);
        using var reader = new StreamReader(stream);
        var xslt = await reader.ReadToEndAsync();
        return Content(xslt, "application/xslt+xml");
    }

    /// <summary>
    /// Ham XSLT + XML ile anlık önizleme — geliştirici mod için.
    /// Request: { xslt, xmlContent }
    /// Response: { html, generationTimeMs }
    /// </summary>
    [HttpPost("raw")]
    [RequestSizeLimit(MaxRawBodyBytes)]
    public IActionResult PreviewRaw([FromBody] RawPreviewRequest request)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(request.Xslt) > MaxRawBodyBytes / 2)
            return BadRequest(new { error = "XSLT içeriği çok büyük (maks. 512 KB)." });
        if (System.Text.Encoding.UTF8.GetByteCount(request.XmlContent) > MaxRawBodyBytes / 2)
            return BadRequest(new { error = "XML içeriği çok büyük (maks. 512 KB)." });

        var sw = Stopwatch.StartNew();
        try
        {
            var html = ApplyXsltWithParams(request.Xslt, request.XmlContent, request.LogoUrl, request.SignatureUrl);

            if (!string.IsNullOrEmpty(request.LogoUrl) || !string.IsNullOrEmpty(request.SignatureUrl))
                html = InjectAssetOverrides(html,
                    request.LogoUrl, request.LogoWidth, request.LogoHeight, request.LogoAlignment,
                    request.SignatureUrl, request.SignatureWidth, request.SignatureHeight, request.SignatureAlignment);

            if (request.BankInfo?.Count > 0)
                html = InjectBankInfo(html, request.BankInfo);

            sw.Stop();
            return Ok(new { html, generationTimeMs = sw.ElapsedMilliseconds });
        }
        catch (XsltException ex)
        {
            return BadRequest(new { error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
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
    /// Block tree'den ham XSLT üretir — dosya indirme için.
    /// Response: raw XSLT text (application/xslt+xml)
    /// </summary>
    [HttpPost("xslt")]
    public IActionResult GenerateXslt([FromBody] PreviewRequest request)
    {
        var tree = new BlockTreeDto { Sections = request.Sections, Blocks = request.Blocks };
        var (xslt, error) = _generator.Generate(tree);
        if (xslt is null)
            return BadRequest(new { error });
        return Content(xslt, "application/xslt+xml");
    }

    /// <summary>XSLT sözdizimini doğrular — geliştirici aracı.</summary>
    [HttpPost("validate-xslt")]
    [RequestSizeLimit(MaxXsltBodyBytes)]
    public IActionResult ValidateXslt([FromBody] ValidateXsltRequest request)
    {
        try
        {
            var transform = new XslCompiledTransform();
            var xsltReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(request.Xslt), xsltReaderSettings);
            var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
            transform.Load(reader, xsltSettings, new XmlUrlResolver());
            return Ok(new { valid = true });
        }
        catch (XsltException ex)
        {
            return Ok(new { valid = false, error = ex.Message, line = ex.LineNumber, column = ex.LinePosition });
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

    private static string ApplyXslt(string xslt, string xmlContent)
        => ApplyXsltWithParams(xslt, xmlContent, null, null);

    private static string ApplyXsltWithParams(string xslt, string xmlContent, string? logoUrl, string? signatureUrl)
    {
        var transform = new XslCompiledTransform();
        var xsltReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var xsltReader = XmlReader.Create(new StringReader(xslt), xsltReaderSettings);
        var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
        transform.Load(xsltReader, xsltSettings, new XmlUrlResolver());

        var xmlReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var xmlDoc = new XmlDocument { XmlResolver = null };
        using var xmlReader = XmlReader.Create(new StringReader(xmlContent), xmlReaderSettings);
        xmlDoc.Load(xmlReader);

        var args = new XsltArgumentList();
        if (!string.IsNullOrEmpty(logoUrl))
            args.AddParam("logo-url", "", logoUrl);
        if (!string.IsNullOrEmpty(signatureUrl))
            args.AddParam("signature-url", "", signatureUrl);

        using var sw = new StringWriter();
        transform.Transform(xmlDoc, args, sw);
        return sw.ToString();
    }

    /// <summary>
    /// Injects a script that overrides logo / signature elements after the XSLT's own JS has run.
    /// Targets all known ID conventions:
    ///   Logo  → company_logo, companyLogo
    ///   İmza  → imza, companyKase
    /// </summary>
    private static string InjectAssetOverrides(
        string html,
        string? logoUrl, int? logoWidth, int? logoHeight, string? logoAlignment,
        string? signatureUrl, int? sigWidth, int? sigHeight, string? sigAlignment)
    {
        // justify-content values for flex container
        static string JustifyContent(string? align) => align switch
        {
            "center" => "center",
            "right" => "flex-end",
            _ => "flex-start",
        };

        // Img inline style: explicit px if provided, otherwise auto; always max-width:100%
        static string ImgStyle(int? w, int? h) =>
            $"{(w.HasValue ? $"width:{w}px;" : "width:auto;")}" +
            $"{(h.HasValue ? $"height:{h}px;" : "height:auto;")}" +
            "max-width:100%;object-fit:contain;display:block;";

        static string BuildSetCall(string[] ids, string url, int? w, int? h, string? align)
        {
            var jc = JustifyContent(align);
            var style = ImgStyle(w, h);
            // Use flexbox on the container so alignment works regardless of img display type
            var fn = $"function(el){{" +
                     $"el.style.cssText='display:flex;justify-content:{jc};align-items:center;';" +
                     $"el.innerHTML='<img src=\"{url}\" style=\"{style}\">';}}";
            var idList = string.Join(",", ids.Select(id => $"'{id}'"));
            return $"setEl([{idList}],{fn});";
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<script>(function(){");
        sb.Append("function setEl(ids,fn){ids.forEach(function(id){var el=document.getElementById(id);if(el)fn(el);});}");
        sb.Append("function applyOverrides(){");

        if (!string.IsNullOrEmpty(logoUrl))
        {
            var safe = logoUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append(BuildSetCall(["company_logo", "companyLogo"], safe, logoWidth, logoHeight, logoAlignment));
        }

        if (!string.IsNullOrEmpty(signatureUrl))
        {
            var safe = signatureUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append(BuildSetCall(["imza", "companyKase"], safe, sigWidth, sigHeight, sigAlignment));
        }

        sb.Append("}window.addEventListener('load',applyOverrides);})();</script>");

        var insertIdx = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return insertIdx >= 0 ? html.Insert(insertIdx, sb.ToString()) : html + sb.ToString();
    }

    private static string InjectBankInfo(string html, List<BankInfoItem> bankInfo)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:12px;margin:16px 0;page-break-inside:avoid\">");
        sb.Append("<table style=\"width:100%;border-collapse:collapse\">");
        sb.Append("<thead><tr>");
        sb.Append("<th style=\"text-align:left;padding:6px 10px;background:#f5f5f5;border:1px solid #ddd\">Banka Adı</th>");
        sb.Append("<th style=\"text-align:left;padding:6px 10px;background:#f5f5f5;border:1px solid #ddd\">IBAN</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var item in bankInfo)
        {
            sb.Append("<tr>");
            sb.Append($"<td style=\"padding:6px 10px;border:1px solid #ddd\">{System.Net.WebUtility.HtmlEncode(item.BankName)}</td>");
            sb.Append($"<td style=\"padding:6px 10px;border:1px solid #ddd;font-family:monospace\">{System.Net.WebUtility.HtmlEncode(item.Iban)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");

        var bankHtml = sb.ToString();
        var insertIdx = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return insertIdx >= 0 ? html.Insert(insertIdx, bankHtml) : html + bankHtml;
    }
}

public class ValidateXsltRequest
{
    public string Xslt { get; set; } = "";
}
