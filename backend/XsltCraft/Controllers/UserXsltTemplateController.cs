using System.Security.Claims;
using System.Xml;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/user-xslt-templates")]
[Authorize]
public class UserXsltTemplateController(
    AppDbContext db,
    IUserActivityRecorder activity,
    IEntitlementService entitlements,
    IFixedNoteInjector noteInjector) : ControllerBase
{
    // Heartbeat'i bu süreden eski olan kilit "pasif" sayılır ve devralınabilir.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(90);

    // Toplu yükleme sınırları (admin tema yüklemesiyle tutarlı).
    private const int MaxBulkItems = 100;
    private const int MaxXsltChars = 2 * 1024 * 1024;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string DisplayNameOf(User user) =>
        string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName!;

    // Kilit, başka bir kullanıcı tarafından ve hâlâ canlı (heartbeat zaman aşımı geçmemiş) mi?
    private static bool IsLockedByOther(UserXsltTemplate t, Guid userId, DateTime now) =>
        t.EditingUserId is not null
        && t.EditingUserId != userId
        && t.EditingHeartbeatAt is not null
        && t.EditingHeartbeatAt > now - LockTimeout;

    // GET /api/user-xslt-templates — sahip olunan + benimle paylaşılan XSLT şablonları
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = CurrentUserId;

        var templates = await db.UserXsltTemplates
            .Where(t => t.OwnerId == userId || t.Shares.Any(s => s.UserId == userId))
            .Include(t => t.Owner)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new UserXsltTemplateResponse
            {
                Id = t.Id,
                Name = t.Name,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                IsOwner = t.OwnerId == userId,
                IsShared = t.OwnerId != userId,
                OwnerId = t.OwnerId,
                OwnerName = string.IsNullOrWhiteSpace(t.Owner.DisplayName) ? t.Owner.Username : t.Owner.DisplayName!,
                // Klasör/favori yalnız sahibin görünümü içindir; paylaşılan şablonlarda gösterilmez.
                FolderId = t.OwnerId == userId ? t.FolderId : null,
                IsFavorite = t.OwnerId == userId && t.IsFavorite
            })
            .ToListAsync();

        return Ok(templates);
    }

    // GET /api/user-xslt-templates/:id — tekil şablonu getir (içerikle birlikte)
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates
            .Include(t => t.Shares)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (!CanAccess(template, userId))
            return Forbid();

        return Ok(new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            IsOwner = template.OwnerId == userId
        });
    }

    // POST /api/user-xslt-templates — yeni XSLT şablonu oluştur
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserXsltRequest request)
    {
        var userId = CurrentUserId;

        // Ham XSLT'yi "Şablonlarım"a kaydetme/saklama Pro üyeliği gerektirir (Standart kullanıcı indirebilir ama saklayamaz).
        var saveGate = await GateSaveAsync();
        if (saveGate is not null) return saveGate;

        if (string.IsNullOrWhiteSpace(request.XsltContent))
            return BadRequest(new { message = "XSLT içeriği boş olamaz." });

        var template = new UserXsltTemplate
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Yeni Şablon" : request.Name.Trim(),
            OwnerId = userId,
            XsltContent = request.XsltContent,
            XmlContent = request.XmlContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.UserXsltTemplates.Add(template);
        await db.SaveChangesAsync();
        await activity.RecordAsync(userId, UserActivityType.Save, template.Id, "Xslt");

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            IsOwner = true
        });
    }

    // POST /api/user-xslt-templates/bulk — klasör olarak toplu XSLT yükle (yalnız sahip; Pro gerekli)
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUpload([FromBody] BulkUploadUserXsltRequest request)
    {
        var userId = CurrentUserId;

        var saveGate = await GateSaveAsync();
        if (saveGate is not null) return saveGate;

        var items = request.Items ?? [];
        if (items.Count == 0)
            return BadRequest(new { message = "Yüklenecek dosya yok." });
        if (items.Count > MaxBulkItems)
            return BadRequest(new { message = $"Tek seferde en fazla {MaxBulkItems} dosya yüklenebilir." });

        // Hedef klasör doğrulaması (MoveToFolder ile aynı: sahip + Kind=XsltTemplate)
        if (request.FolderId is not null)
        {
            var folder = await db.Folders.FindAsync(request.FolderId.Value);
            if (folder is null || folder.OwnerId != userId || folder.Kind != FolderKind.XsltTemplate)
                return BadRequest(new { message = "Geçersiz klasör." });
        }

        var response = new BulkUploadResultResponse();
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var name = string.IsNullOrWhiteSpace(item.Name) ? "Yeni Şablon" : item.Name.Trim();
            var reason = ValidateXslt(item.XsltContent);
            if (reason is not null)
            {
                response.Skipped.Add(new BulkUploadSkipped { Name = name, Reason = reason });
                continue;
            }

            var template = new UserXsltTemplate
            {
                Id = Guid.NewGuid(),
                Name = name,
                OwnerId = userId,
                XsltContent = item.XsltContent,
                FolderId = request.FolderId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.UserXsltTemplates.Add(template);
            response.Created.Add(new BulkUploadCreated { Id = template.Id, Name = template.Name });
        }

        if (response.Created.Count > 0)
        {
            await db.SaveChangesAsync();
            foreach (var created in response.Created)
                await activity.RecordAsync(userId, UserActivityType.Save, created.Id, "Xslt");
        }

        return Ok(response);
    }

    // POST /api/user-xslt-templates/bulk-add-note — seçili şablonların not bölümüne sabit not göm (yalnız sahip; Pro)
    [HttpPost("bulk-add-note")]
    public async Task<IActionResult> BulkAddFixedNote([FromBody] BulkAddFixedNoteRequest request)
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var saveGate = await GateSaveAsync();
        if (saveGate is not null) return saveGate;

        var noteText = request.NoteText?.Trim() ?? string.Empty;
        if (noteText.Length == 0)
            return BadRequest(new { message = "Not metni boş olamaz." });
        if (noteText.Length > 1000)
            return BadRequest(new { message = "Not metni 1000 karakteri aşamaz." });

        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { message = "Şablon seçilmedi." });

        var mode = string.Equals(request.Mode, "append", StringComparison.OrdinalIgnoreCase)
            ? FixedNoteMode.Append
            : FixedNoteMode.Replace;

        // Yalnız sahip olunan şablonlar (IDOR koruması).
        var templates = await db.UserXsltTemplates
            .Where(t => ids.Contains(t.Id) && t.OwnerId == userId)
            .ToListAsync();

        var response = new BulkAddFixedNoteResultResponse();
        var changed = false;

        foreach (var template in templates)
        {
            if (IsLockedByOther(template, userId, now))
            {
                response.Results.Add(new BulkAddFixedNoteItemResult { Id = template.Id, Name = template.Name, Status = "locked" });
                continue;
            }

            var result = noteInjector.Inject(template.XsltContent, noteText, mode);
            switch (result.Status)
            {
                case FixedNoteStatus.Updated:
                    template.XsltContent = result.Xslt;
                    template.UpdatedAt = now;
                    changed = true;
                    response.Results.Add(new BulkAddFixedNoteItemResult { Id = template.Id, Name = template.Name, Status = "updated" });
                    break;
                case FixedNoteStatus.NoNotesSection:
                    response.Results.Add(new BulkAddFixedNoteItemResult { Id = template.Id, Name = template.Name, Status = "no_notes" });
                    break;
                default:
                    response.Results.Add(new BulkAddFixedNoteItemResult { Id = template.Id, Name = template.Name, Status = "failed" });
                    break;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
            foreach (var r in response.Results.Where(r => r.Status == "updated"))
                await activity.RecordAsync(userId, UserActivityType.Save, r.Id, "Xslt");
        }

        return Ok(response);
    }

    // PUT /api/user-xslt-templates/:id — şablonu güncelle (sahip veya paylaşılan kullanıcı)
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserXsltRequest request)
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var template = await db.UserXsltTemplates
            .Include(t => t.Shares)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (!CanAccess(template, userId))
            return Forbid();

        // Kaydetme/saklama Pro üyeliği gerektirir (downgrade olmuş ya da paylaşılan Standart kullanıcı dahil).
        var saveGate = await GateSaveAsync();
        if (saveGate is not null) return saveGate;

        // Başka kullanıcı aktif olarak düzenliyorsa kaydetmeyi engelle
        if (IsLockedByOther(template, userId, now))
        {
            var editor = await db.Users.FindAsync(template.EditingUserId!.Value);
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = editor is not null
                    ? $"Şu an {DisplayNameOf(editor)} bu şablonu düzenliyor. Kaydedilemedi."
                    : "Bu şablon şu an başka bir kullanıcı tarafından düzenleniyor. Kaydedilemedi."
            });
        }

        // İyimser çakışma kontrolü (UTC üzerinden, 1 sn tolerans ile)
        if (request.ExpectedUpdatedAt is not null)
        {
            var dbUpdatedUtc = DateTime.SpecifyKind(template.UpdatedAt, DateTimeKind.Utc);
            if (dbUpdatedUtc > request.ExpectedUpdatedAt.Value.UtcDateTime.AddSeconds(1))
                return Conflict(new { message = "Şablon siz düzenlerken değiştirildi. Lütfen yeniden yükleyin." });
        }

        if (request.Name is not null)
            template.Name = request.Name.Trim();

        if (request.XsltContent is not null)
            template.XsltContent = request.XsltContent;

        if (request.XmlContent is not null)
            template.XmlContent = request.XmlContent;

        template.UpdatedAt = now;
        // Kaydeden kullanıcının kilidini tazele
        template.EditingUserId = userId;
        template.EditingHeartbeatAt = now;

        await db.SaveChangesAsync();
        await activity.RecordAsync(userId, UserActivityType.Save, template.Id, "Xslt");

        return Ok(new UserXsltTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            XsltContent = template.XsltContent,
            XmlContent = template.XmlContent,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            IsOwner = template.OwnerId == userId
        });
    }

    // DELETE /api/user-xslt-templates/:id — şablonu sil (yalnızca sahip)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });

        if (template.OwnerId != userId)
            return Forbid();

        db.UserXsltTemplates.Remove(template);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/user-xslt-templates/:id/shares — paylaşılan kullanıcılar (yalnızca sahip)
    [HttpGet("{id:guid}/shares")]
    public async Task<IActionResult> GetShares(Guid id)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (template.OwnerId != userId)
            return Forbid();

        var shares = await db.UserXsltTemplateShares
            .Where(s => s.TemplateId == id)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new TemplateShareResponse
            {
                UserId = s.UserId,
                Username = s.User.Username,
                DisplayName = s.User.DisplayName,
                Email = s.User.Email
            })
            .ToListAsync();

        return Ok(shares);
    }

    // POST /api/user-xslt-templates/:id/shares — şablonu bir kullanıcıyla paylaş (yalnızca sahip)
    [HttpPost("{id:guid}/shares")]
    public async Task<IActionResult> AddShare(Guid id, [FromBody] ShareTemplateRequest request)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (template.OwnerId != userId)
            return Forbid();

        if (request.UserId == userId)
            return BadRequest(new { message = "Şablon zaten size ait." });

        var targetUser = await db.Users.FindAsync(request.UserId);
        if (targetUser is null)
            return NotFound(new { message = "Kullanıcı bulunamadı." });

        var exists = await db.UserXsltTemplateShares
            .AnyAsync(s => s.TemplateId == id && s.UserId == request.UserId);
        if (exists)
            return Conflict(new { message = "Bu kullanıcıyla zaten paylaşıldı." });

        db.UserXsltTemplateShares.Add(new UserXsltTemplateShare
        {
            Id = Guid.NewGuid(),
            TemplateId = id,
            UserId = request.UserId,
            GrantedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok(new TemplateShareResponse
        {
            UserId = targetUser.Id,
            Username = targetUser.Username,
            DisplayName = targetUser.DisplayName,
            Email = targetUser.Email
        });
    }

    // DELETE /api/user-xslt-templates/:id/shares/:userId — paylaşımı kaldır (yalnızca sahip)
    [HttpDelete("{id:guid}/shares/{shareUserId:guid}")]
    public async Task<IActionResult> RemoveShare(Guid id, Guid shareUserId)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (template.OwnerId != userId)
            return Forbid();

        var share = await db.UserXsltTemplateShares
            .FirstOrDefaultAsync(s => s.TemplateId == id && s.UserId == shareUserId);
        if (share is null)
            return NoContent();

        db.UserXsltTemplateShares.Remove(share);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // POST /api/user-xslt-templates/:id/lock/acquire — kilidi al/tazele (heartbeat)
    [HttpPost("{id:guid}/lock/acquire")]
    public async Task<IActionResult> AcquireLock(Guid id)
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var template = await db.UserXsltTemplates
            .Include(t => t.Shares)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (!CanAccess(template, userId))
            return Forbid();

        if (IsLockedByOther(template, userId, now))
        {
            var editor = await db.Users.FindAsync(template.EditingUserId!.Value);
            return Ok(new EditingLockResponse
            {
                LockedByOther = true,
                EditingUserId = template.EditingUserId,
                EditingUserName = editor is not null ? DisplayNameOf(editor) : null,
                UpdatedAt = template.UpdatedAt
            });
        }

        // Kilit boş veya bana ait (ya da zaman aşımına uğramış) → kendime al/tazele
        template.EditingUserId = userId;
        template.EditingHeartbeatAt = now;
        await db.SaveChangesAsync();

        return Ok(new EditingLockResponse
        {
            LockedByOther = false,
            EditingUserId = userId,
            EditingUserName = null,
            UpdatedAt = template.UpdatedAt
        });
    }

    // POST /api/user-xslt-templates/:id/lock/release — kilidi serbest bırak (yalnızca sahibi)
    [HttpPost("{id:guid}/lock/release")]
    public async Task<IActionResult> ReleaseLock(Guid id)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NoContent();

        if (template.EditingUserId == userId)
        {
            template.EditingUserId = null;
            template.EditingHeartbeatAt = null;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    // PATCH /api/user-xslt-templates/:id/folder — şablonu klasöre taşı / çıkar (yalnız sahip)
    [HttpPatch("{id:guid}/folder")]
    public async Task<IActionResult> MoveToFolder(Guid id, [FromBody] MoveToFolderRequest request)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (template.OwnerId != userId)
            return Forbid();

        if (request.FolderId is not null)
        {
            var folder = await db.Folders.FindAsync(request.FolderId.Value);
            if (folder is null || folder.OwnerId != userId || folder.Kind != FolderKind.XsltTemplate)
                return BadRequest(new { message = "Geçersiz klasör." });
        }

        template.FolderId = request.FolderId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/user-xslt-templates/:id/favorite — favori durumunu değiştir (yalnız sahip)
    [HttpPatch("{id:guid}/favorite")]
    public async Task<IActionResult> SetFavorite(Guid id, [FromBody] SetFavoriteRequest request)
    {
        var userId = CurrentUserId;

        var template = await db.UserXsltTemplates.FindAsync(id);
        if (template is null)
            return NotFound(new { message = "Şablon bulunamadı." });
        if (template.OwnerId != userId)
            return Forbid();

        template.IsFavorite = request.IsFavorite;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static bool CanAccess(UserXsltTemplate template, Guid userId) =>
        template.OwnerId == userId || template.Shares.Any(s => s.UserId == userId);

    /// <summary>
    /// Toplu yüklemede her XSLT'yi fail-closed doğrular: boyut, iyi-biçimli XML (XXE kapalı) ve
    /// <see cref="XsltSafety"/> taraması. Sorun varsa açıklayıcı sebep; temizse null.
    /// </summary>
    private static string? ValidateXslt(string? xslt)
    {
        if (string.IsNullOrWhiteSpace(xslt))
            return "XSLT içeriği boş.";
        if (xslt.Length > MaxXsltChars)
            return "Dosya çok büyük (2 MB sınırı).";

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var sr = new StringReader(xslt);
            using var reader = XmlReader.Create(sr, settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            return $"Geçersiz XML: {ex.Message}";
        }

        return XsltSafety.FindThreat(xslt);
    }

    /// <summary>
    /// Ham XSLT kaydetme/saklama yetki kapısı. İzinliyse null; değilse 402 (Pro'ya yönlendir).
    /// Editör/Admin ve Pro izinli; Standart (Free) değil.
    /// </summary>
    private async Task<IActionResult?> GateSaveAsync()
    {
        var ent = await entitlements.GetAsync(CurrentUserId);
        if (ent.CanSaveRawXslt) return null;
        return StatusCode(StatusCodes.Status402PaymentRequired,
            new { error = "upgrade_required", upgrade = true, message = "Şablon kaydetme ve 'Şablonlarım'da saklama XsltCraft Pro üyeliği gerektirir. XSLT'yi indirebilirsiniz ancak saklamak için Pro'ya geçin." });
    }
}
