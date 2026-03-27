using System.Xml;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, IStorageService storage) : ControllerBase
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private const long MaxThumbnailSizeBytes = 1 * 1024 * 1024; // 1 MB

    private static readonly string[] AllowedThumbnailExtensions = [".png", ".jpg", ".jpeg", ".gif", ".svg"];
    private static readonly string[] AllowedThumbnailMimeTypes = ["image/png", "image/jpeg", "image/gif", "image/svg+xml"];

    // POST /api/admin/themes
    [HttpPost("themes")]
    public async Task<IActionResult> CreateTheme(
        [FromForm] string name,
        [FromForm] string documentType,
        IFormFile file,
        IFormFile? thumbnailFile = null)
    {
        var validationError = await ValidateXsltFileAsync(file);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            return BadRequest(new { message = "Geçersiz documentType. Geçerli değerler: Invoice, Despatch" });

        var themeId = Guid.NewGuid();
        var relativePath = $"themes/{themeId}.xslt";

        await using (var stream = file.OpenReadStream())
            await storage.WriteAsync(stream, relativePath, "application/xslt+xml");

        string? thumbnailUrl = null;
        if (thumbnailFile is not null)
        {
            var thumbError = ValidateThumbnailFile(thumbnailFile);
            if (thumbError is not null)
                return BadRequest(new { message = thumbError });

            var thumbExt = Path.GetExtension(thumbnailFile.FileName).ToLowerInvariant();
            var thumbPath = $"themes/thumbnails/{themeId}{thumbExt}";
            await using var thumbStream = thumbnailFile.OpenReadStream();
            await storage.WriteAsync(thumbStream, thumbPath, thumbnailFile.ContentType);
            thumbnailUrl = thumbPath;
        }

        var template = new Template
        {
            Id = themeId,
            Name = name,
            DocumentType = docType,
            IsFreeTheme = true,
            XsltStoragePath = relativePath,
            ThumbnailUrl = thumbnailUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTheme), new { id = themeId }, ToResponse(template));
    }

    // GET /api/admin/themes/:id  (helper for CreatedAtAction)
    [HttpGet("themes/{id:guid}")]
    public async Task<IActionResult> GetTheme(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        return template is null ? NotFound() : Ok(ToResponse(template));
    }

    // PUT /api/admin/themes/:id
    [HttpPut("themes/{id:guid}")]
    public async Task<IActionResult> UpdateTheme(
        Guid id,
        [FromForm] string? name,
        [FromForm] string? documentType,
        IFormFile? file = null)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound();

        if (file is not null)
        {
            var validationError = await ValidateXsltFileAsync(file);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            // Eskiyi sil, yeniyi yaz
            if (!string.IsNullOrEmpty(template.XsltStoragePath))
                await storage.DeleteAsync(template.XsltStoragePath);

            var relativePath = $"themes/{id}.xslt";
            await using var stream = file.OpenReadStream();
            await storage.WriteAsync(stream, relativePath, "application/xslt+xml");
            template.XsltStoragePath = relativePath;
        }

        if (!string.IsNullOrWhiteSpace(name))
            template.Name = name;

        if (!string.IsNullOrWhiteSpace(documentType) &&
            Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            template.DocumentType = docType;

        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToResponse(template));
    }

    // DELETE /api/admin/themes/:id
    [HttpDelete("themes/{id:guid}")]
    public async Task<IActionResult> DeleteTheme(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound();

        if (!string.IsNullOrEmpty(template.XsltStoragePath))
            await storage.DeleteAsync(template.XsltStoragePath);

        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // -------------------------------------------------------

    private static string? ValidateThumbnailFile(IFormFile file)
    {
        if (file.Length == 0)
            return "Thumbnail dosyası boş olamaz.";

        if (file.Length > MaxThumbnailSizeBytes)
            return "Thumbnail boyutu 1 MB sınırını aşıyor.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedThumbnailExtensions.Contains(ext))
            return "Thumbnail için yalnızca PNG, JPG, GIF ve SVG dosyaları kabul edilir.";

        if (!AllowedThumbnailMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            return "Geçersiz thumbnail MIME türü.";

        return null;
    }

    private static readonly string[] AllowedXsltMimeTypes =
        ["application/xslt+xml", "text/xml", "application/xml", "application/octet-stream"];

    private static async Task<string?> ValidateXsltFileAsync(IFormFile file)
    {
        if (file.Length == 0)
            return "Dosya boş olamaz.";

        if (file.Length > MaxFileSizeBytes)
            return "Dosya boyutu 2 MB sınırını aşıyor.";

        var ext = Path.GetExtension(file.FileName);
        if (!ext.Equals(".xslt", StringComparison.OrdinalIgnoreCase))
            return "Yalnızca .xslt uzantılı dosyalar kabul edilir.";

        if (!AllowedXsltMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            return "Geçersiz MIME türü.";

        // XML içerik doğrulaması — XXE korumalı
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var xmlReader = XmlReader.Create(new StringReader(content), xmlSettings);

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType != XmlNodeType.Element) continue;

                const string xsltNamespace = "http://www.w3.org/1999/XSL/Transform";
                if (xmlReader.NamespaceURI != xsltNamespace ||
                    xmlReader.LocalName is not ("stylesheet" or "transform"))
                    return "Dosya geçerli bir XSLT şablonu değil (xsl:stylesheet root elementi bulunamadı).";

                return null; // geçerli XSLT
            }

            return "Dosya boş veya geçersiz içerik barındırıyor.";
        }
        catch (XmlException)
        {
            return "Dosya geçerli bir XML/XSLT içermiyor.";
        }
    }

    private static FreeThemeResponse ToResponse(Template t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        DocumentType = t.DocumentType.ToString(),
        ThumbnailUrl = t.ThumbnailUrl,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
