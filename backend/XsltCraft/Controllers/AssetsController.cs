using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetsController(AppDbContext db, IStorageService storage) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".svg"];

    // POST /api/assets/upload
    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string assetType = "Custom")
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Dosya boş olamaz." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "Dosya boyutu 5 MB sınırını aşıyor." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Yalnızca PNG, JPG ve SVG dosyaları kabul edilir." });

        if (!Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var assetTypeEnum))
            assetTypeEnum = AssetType.Custom;

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var assetId = Guid.NewGuid();
        var relativePath = $"assets/{userId}/{assetId}{ext}";

        await using (var stream = file.OpenReadStream())
            await storage.WriteAsync(stream, relativePath, file.ContentType);

        var asset = new Asset
        {
            Id = assetId,
            OwnerId = userId,
            Type = assetTypeEnum,
            FilePath = relativePath,
            MimeType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        return Ok(new
        {
            id = assetId,
            url = $"/api/assets/{assetId}/serve",
            storagePath = relativePath,
            type = assetTypeEnum.ToString(),
            mimeType = file.ContentType,
            sizeBytes = file.Length
        });
    }

    // GET /api/assets/{id}/serve — XSLT içi <img> tag'larından erişilebilmesi için auth zorunlu değil
    [HttpGet("{id:guid}/serve")]
    [AllowAnonymous]
    public async Task<IActionResult> Serve(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        if (!await storage.ExistsAsync(asset.FilePath))
            return NotFound(new { message = "Dosya storage'da bulunamadı." });

        var stream = await storage.ReadAsync(asset.FilePath);
        return File(stream, asset.MimeType);
    }

    // DELETE /api/assets/{id}
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (asset.OwnerId != userId)
            return Forbid();

        await storage.DeleteAsync(asset.FilePath);
        db.Assets.Remove(asset);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
