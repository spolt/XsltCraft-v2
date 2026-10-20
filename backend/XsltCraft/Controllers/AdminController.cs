using System.Security.Claims;
using System.Xml;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Imaging;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    AppDbContext db,
    IStorageService storage,
    IThemeUploadProcessor themeUploads,
    IThemeSlotProjector slotProjector) : ControllerBase
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private const long MaxThumbnailSizeBytes = 1 * 1024 * 1024; // 1 MB


    // POST /api/admin/themes
    [HttpPost("themes")]
    public async Task<IActionResult> CreateTheme(
        [FromForm] string name,
        [FromForm] string documentType,
        IFormFile file,
        [FromForm] bool isPremium = false,
        IFormFile? thumbnailFile = null,
        [FromForm] string? existingOverrides = null,
        [FromForm] string? existingImages = null)
    {
        var (upload, validationError) = await ReadAndValidateXsltAsync(file);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            return BadRequest(new { message = "Geçersiz documentType. Geçerli değerler: Invoice, Despatch" });

        if (!TryParseDecision(existingOverrides, out var decision))
            return BadRequest(new { message = "Geçersiz existingOverrides. Geçerli değerler: strip, keep" });
        if (!TryParseImageRoles(existingImages, out var imageRoles))
            return BadRequest(new { message = ImageRolesError });

        (byte[]? Bytes, ImageUploadResult? Image, string? Error) thumbnail = default;
        if (thumbnailFile is not null)
        {
            thumbnail = await ReadThumbnailAsync(thumbnailFile);
            if (thumbnail.Error is not null)
                return BadRequest(new { message = thumbnail.Error });
        }

        // Analiz + (varsa) önceden gömülü görsel bloğu kararı — hiçbir şey yazılmadan önce.
        var outcome = themeUploads.Process(upload!.Text, docType, decision, imageRoles);
        if (OutcomeError(outcome) is { } outcomeError)
            return outcomeError;
        var ready = (ThemeUploadOutcome.Ready)outcome;

        var themeId = Guid.NewGuid();
        var relativePath = $"themes/{themeId}.xslt";
        await WriteThemeAsync(relativePath, ready, upload.Bytes);

        string? thumbnailUrl = null;
        if (thumbnail.Bytes is not null)
        {
            var thumbPath = $"themes/thumbnails/{themeId}{thumbnail.Image!.Extension}";
            await using var thumbStream = new MemoryStream(thumbnail.Bytes, writable: false);
            await storage.WriteAsync(thumbStream, thumbPath, thumbnail.Image.MimeType);
            thumbnailUrl = thumbPath;
        }

        var template = new Template
        {
            Id = themeId,
            Name = name,
            DocumentType = docType,
            IsFreeTheme = true,
            IsPremium = isPremium,
            XsltStoragePath = relativePath,
            SlotCatalog = ready.CatalogJson,
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
        [FromForm] bool? isPremium = null,
        IFormFile? file = null,
        [FromForm] string? existingOverrides = null,
        [FromForm] string? existingImages = null)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null)
            return NotFound();

        if (isPremium.HasValue)
            template.IsPremium = isPremium.Value;

        if (!string.IsNullOrWhiteSpace(documentType) &&
            Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            template.DocumentType = docType;

        if (file is not null)
        {
            var (upload, validationError) = await ReadAndValidateXsltAsync(file);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            if (!TryParseDecision(existingOverrides, out var decision))
                return BadRequest(new { message = "Geçersiz existingOverrides. Geçerli değerler: strip, keep" });
            if (!TryParseImageRoles(existingImages, out var imageRoles))
                return BadRequest(new { message = ImageRolesError });

            // Önce işle (409/422 dönebilir), eski dosya ancak sonra silinir.
            var outcome = themeUploads.Process(upload!.Text, template.DocumentType, decision, imageRoles);
            if (OutcomeError(outcome) is { } outcomeError)
                return outcomeError;
            var ready = (ThemeUploadOutcome.Ready)outcome;

            if (!string.IsNullOrEmpty(template.XsltStoragePath))
                await storage.DeleteAsync(template.XsltStoragePath);

            var relativePath = $"themes/{id}.xslt";
            await WriteThemeAsync(relativePath, ready, upload.Bytes);
            template.XsltStoragePath = relativePath;
            template.SlotCatalog = ready.CatalogJson;
        }

        if (!string.IsNullOrWhiteSpace(name))
            template.Name = name;

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

    // ─── Admin Snippet Endpoints ────────────────────────────────────────────────

    private static readonly HashSet<string> AllowedSnippetScopes = ["xslt", "xpath", "html"];

    // GET /api/admin/snippets
    [HttpGet("snippets")]
    public async Task<IActionResult> GetSnippets()
    {
        var snippets = await db.UserSnippets
            .Where(s => s.IsPublic)
            .OrderBy(s => s.Scope)
            .ThenBy(s => s.Prefix)
            .Select(s => new UserSnippetResponse
            {
                Id = s.Id,
                Prefix = s.Prefix,
                Body = s.Body,
                Description = s.Description,
                Scope = s.Scope,
                IsPublic = s.IsPublic,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync();

        return Ok(snippets);
    }

    // POST /api/admin/snippets
    [HttpPost("snippets")]
    public async Task<IActionResult> CreateSnippet([FromBody] CreateUserSnippetRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (string.IsNullOrWhiteSpace(request.Prefix))
            return BadRequest(new { message = "Prefix boş olamaz." });

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Snippet içeriği boş olamaz." });

        var scope = request.Scope?.ToLowerInvariant() ?? "xslt";
        if (!AllowedSnippetScopes.Contains(scope))
            return BadRequest(new { message = "Geçersiz scope. Kabul edilenler: xslt, xpath, html." });

        var snippet = new UserSnippet
        {
            Id = Guid.NewGuid(),
            OwnerId = adminId,
            Prefix = request.Prefix.Trim(),
            Body = request.Body,
            Description = request.Description?.Trim(),
            Scope = scope,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.UserSnippets.Add(snippet);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSnippets), null, ToSnippetResponse(snippet));
    }

    // PUT /api/admin/snippets/:id
    [HttpPut("snippets/{id:guid}")]
    public async Task<IActionResult> UpdateSnippet(Guid id, [FromBody] UpdateUserSnippetRequest request)
    {
        var snippet = await db.UserSnippets.FindAsync(id);
        if (snippet is null) return NotFound(new { message = "Snippet bulunamadı." });
        if (!snippet.IsPublic) return Forbid();

        if (request.Prefix is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest(new { message = "Prefix boş olamaz." });
            snippet.Prefix = request.Prefix.Trim();
        }

        if (request.Body is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { message = "Snippet içeriği boş olamaz." });
            snippet.Body = request.Body;
        }

        if (request.Description is not null)
            snippet.Description = request.Description.Trim();

        if (request.Scope is not null)
        {
            var scope = request.Scope.ToLowerInvariant();
            if (!AllowedSnippetScopes.Contains(scope))
                return BadRequest(new { message = "Geçersiz scope. Kabul edilenler: xslt, xpath, html." });
            snippet.Scope = scope;
        }

        snippet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToSnippetResponse(snippet));
    }

    // DELETE /api/admin/snippets/:id
    [HttpDelete("snippets/{id:guid}")]
    public async Task<IActionResult> DeleteSnippet(Guid id)
    {
        var snippet = await db.UserSnippets.FindAsync(id);
        if (snippet is null) return NotFound(new { message = "Snippet bulunamadı." });
        if (!snippet.IsPublic) return Forbid();

        db.UserSnippets.Remove(snippet);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static UserSnippetResponse ToSnippetResponse(UserSnippet s) => new()
    {
        Id = s.Id,
        Prefix = s.Prefix,
        Body = s.Body,
        Description = s.Description,
        Scope = s.Scope,
        IsPublic = s.IsPublic,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    // -------------------------------------------------------

    /// <summary>Thumbnail: yalnız PNG/JPG/JPEG, içerik uzantıyla eşleşmeli (ortak <see cref="ImageUpload"/> kuralı).</summary>
    private static async Task<(byte[]? Bytes, ImageUploadResult? Image, string? Error)> ReadThumbnailAsync(IFormFile file)
    {
        if (file.Length == 0)
            return (null, null, "Thumbnail dosyası boş olamaz.");
        if (file.Length > MaxThumbnailSizeBytes)
            return (null, null, "Thumbnail boyutu 1 MB sınırını aşıyor.");

        await using var input = file.OpenReadStream();
        using var ms = new MemoryStream((int)file.Length);
        await input.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var image = ImageUpload.Validate(bytes, file.FileName);
        return image.IsValid ? (bytes, image, null) : (null, null, $"Thumbnail: {image.Error}");
    }

    private static readonly string[] AllowedXsltMimeTypes =
        ["application/xslt+xml", "text/xml", "application/xml", "application/octet-stream"];

    private sealed record XsltUpload(string Text, byte[] Bytes);

    private static async Task<(XsltUpload? Upload, string? Error)> ReadAndValidateXsltAsync(IFormFile file)
    {
        if (file.Length == 0)
            return (null, "Dosya boş olamaz.");

        if (file.Length > MaxFileSizeBytes)
            return (null, "Dosya boyutu 2 MB sınırını aşıyor.");

        var ext = Path.GetExtension(file.FileName);
        if (!ext.Equals(".xslt", StringComparison.OrdinalIgnoreCase))
            return (null, "Yalnızca .xslt uzantılı dosyalar kabul edilir.");

        if (!AllowedXsltMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            return (null, "Geçersiz MIME türü.");

        byte[] bytes;
        await using (var input = file.OpenReadStream())
        using (var ms = new MemoryStream((int)file.Length))
        {
            await input.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        string content;
        using (var reader = new StreamReader(new MemoryStream(bytes, writable: false)))
            content = await reader.ReadToEndAsync();

        var error = ValidateXsltContent(content);
        return error is null ? (new XsltUpload(content, bytes), null) : (null, error);
    }

    private static string? ValidateXsltContent(string content)
    {
        // XML içerik doğrulaması — XXE korumalı
        try
        {
            // Güvenlik: ham tema render edilirken Saxon document()/doc()/unparsed-text()'i
            // çalıştırır → SSRF/dosya okuma. Bu fonksiyonları ve harici import'ları reddet.
            var threat = XsltCraft.Application.Validation.XsltSafety.FindThreat(content);
            if (threat is not null)
                return threat;

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

    private AdminThemeResponse ToResponse(Template t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        DocumentType = t.DocumentType.ToString(),
        IsPremium = t.IsPremium,
        ThumbnailUrl = t.ThumbnailUrl,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        SlotAnalysis = slotProjector.ForAdmin(t.SlotCatalog, t.DocumentType),
    };

    private static bool TryParseDecision(string? value, out OverridesDecision? decision)
    {
        decision = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (value.Equals("strip", StringComparison.OrdinalIgnoreCase)) { decision = OverridesDecision.Strip; return true; }
        if (value.Equals("keep", StringComparison.OrdinalIgnoreCase)) { decision = OverridesDecision.Keep; return true; }
        return false;
    }

    private const string ImageRolesError =
        "Geçersiz existingImages. Virgülle ayrılmış değerler: logo, signature, design; ':removable' (logo/signature) ya da ':strip' eklenebilir";

    // "logo:removable,signature:strip,design" → 409 yanıtındaki görsel sırasıyla kararlar. ':removable' → kullanıcı kendi
    // görselini eklemeden temadaki görseli kaldırabilir; ':strip' → admin görseli temadan kaldırır (logo/imzanın yeri
    // konum olarak kalır). Boş → karar verilmedi.
    private static bool TryParseImageRoles(string? value, out IReadOnlyList<ThemeImageDecision>? decisions)
    {
        decisions = null;
        if (value is null)
            return true;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length > 100)
            return false;
        var list = new List<ThemeImageDecision>();
        foreach (var part in parts.Where(p => p.Length > 0))
        {
            var (name, modifier) = part.IndexOf(':') is var i and >= 0 ? (part[..i], part[(i + 1)..]) : (part, null);
            if (!ThemeSlotProjector.TryParseRole(name, out var role))
                return false;
            if (modifier is not (null or "removable" or "strip") || (modifier == "removable" && role == ThemeImageRole.Design))
                return false;
            list.Add(new ThemeImageDecision(role, Removable: modifier == "removable", Strip: modifier == "strip"));
        }
        decisions = list;
        return true;
    }

    // 409: admin kararı gerekli (önceden gömülü görsel bloğu ve/veya temadaki statik görsellerin rolü);
    // 422: karar uygulanamıyor (ör. "koru", analiz başarısızken görseli kaldırma) ya da işlenen tema son denetimden geçmedi.
    private IActionResult? OutcomeError(ThemeUploadOutcome outcome) => outcome switch
    {
        ThemeUploadOutcome.DecisionRequired d => Conflict(new
        {
            message = d.Inspection is not null
                ? "Temada önceden gömülü görseller var (logo/imza bloğu). Kaldırılsın mı, temanın parçası olarak korunsun mu?"
                : "Temada görseller bulundu. Hangisi firma logosu, hangisi imza/kaşe, hangisi tasarımın parçası?",
            code = "upload_decision_required",
            images = d.Images.Select(i => new
            {
                i.Index,
                Suggested = ThemeSlotProjector.RoleValue(i.Suggested),
                i.Reason,
                i.Line,
                i.PreviewDataUrl,
            }),
            overrides = d.Inspection is not { } inspection ? null : new
            {
                inspection.Present,
                inspection.BlockCount,
                inspection.DanglingMarkers,
                inspection.Version,
                inspection.Keepable,
                inspection.Reason,
                Images = inspection.Images.Select(i => new
                {
                    Kind = i.Kind switch { ThemeAssetKind.Logo => "logo", ThemeAssetKind.Signature => "signature", _ => null },
                    i.Targets, i.DataUrl, i.Width, i.Height, i.Alignment, i.Valid, i.Problem,
                }),
                inspection.BankRows,
            },
        }),
        ThemeUploadOutcome.Rejected r => UnprocessableEntity(new { message = r.Message }),
        _ => null,
    };

    // İşlenmiş tema UTF-8 (BOM'suz) yazılır; analiz atlandıysa (UTF-8 dışı bildirim) özgün baytlar.
    private async Task WriteThemeAsync(string relativePath, ThemeUploadOutcome.Ready ready, byte[] originalBytes)
    {
        var bytes = ready.Original ? originalBytes : new System.Text.UTF8Encoding(false).GetBytes(ready.Xslt);
        await using var stream = new MemoryStream(bytes, writable: false);
        await storage.WriteAsync(stream, relativePath, "application/xslt+xml");
    }
}
