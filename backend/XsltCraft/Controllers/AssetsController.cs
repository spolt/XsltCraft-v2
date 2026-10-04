using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.Imaging;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetsController(AppDbContext db, IStorageService storage) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Servis edilen dosya script çalıştıramasın: içerik tipi koklanmaz, doküman olarak açılırsa
    /// (doğrudan URL) sandbox'ta ve kaynaksız kalır. Eski SVG asset'ler için zorunlu; PNG/JPEG'de zararsız.
    /// </summary>
    private const string ServeCsp = "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; sandbox";

    // POST /api/assets/upload
    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string assetType = "Custom")
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Dosya boş olamaz." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "Dosya boyutu 5 MB sınırını aşıyor." });

        // İçerik doğrulaması: uzantı + magic byte + boyut. MIME/uzantı istemci beyanından değil içerikten.
        byte[] bytes;
        await using (var input = file.OpenReadStream())
        using (var ms = new MemoryStream((int)file.Length))
        {
            await input.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        var image = ImageUpload.Validate(bytes, file.FileName);
        if (!image.IsValid)
            return BadRequest(new { message = image.Error });

        if (!Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var assetTypeEnum))
            assetTypeEnum = AssetType.Custom;

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var assetId = Guid.NewGuid();
        var relativePath = $"assets/{userId}/{assetId}{image.Extension}";

        await using (var stream = new MemoryStream(bytes, writable: false))
            await storage.WriteAsync(stream, relativePath, image.MimeType);

        var asset = new Asset
        {
            Id = assetId,
            OwnerId = userId,
            Type = assetTypeEnum,
            FilePath = relativePath,
            MimeType = image.MimeType,
            SizeBytes = bytes.Length,
            CreatedAt = DateTime.UtcNow
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        return Ok(new
        {
            id = assetId,
            url = $"/api/assets/{assetId}/serve",
            type = assetTypeEnum.ToString(),
            mimeType = image.MimeType,
            sizeBytes = bytes.Length
        });
    }

    // GET /api/assets/{id}/serve — XSLT içi <img> tag'larından erişilebilmesi için auth zorunlu değil.
    // Ancak kimlik doğrulanmış kullanıcı başkasının asset'ine erişemez.
    [HttpGet("{id:guid}/serve")]
    [AllowAnonymous]
    public async Task<IActionResult> Serve(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId) && asset.OwnerId != userId)
            return Forbid();

        if (!await storage.ExistsAsync(asset.FilePath))
            return NotFound(new { message = "Dosya storage'da bulunamadı." });

        // MIME DB'deki (eski kayıtlarda istemci beyanı) değerden değil, sunucunun yazdığı uzantıdan.
        var mimeType = ImageUpload.ServeMimeType(asset.FilePath);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = ServeCsp;

        var stream = await storage.ReadAsync(asset.FilePath);
        return mimeType == "application/octet-stream"
            ? File(stream, mimeType, Path.GetFileName(asset.FilePath))   // tanınmayan → indirme, render yok
            : File(stream, mimeType);
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
