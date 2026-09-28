using System.Security.Claims;
using System.Text.RegularExpressions;

using Google.Apis.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IJwtService jwtService,
    IRefreshTokenService refreshTokens,
    IConfiguration configuration) : ControllerBase
{
    private const string RefreshTokenCookie = "refreshToken";
    // Çerez yalnız auth uçlarına gider; diğer API isteklerinde taşınmaz.
    private const string RefreshTokenCookiePath = "/api/auth";

    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_][a-zA-Z0-9_.]{1,28}[a-zA-Z0-9_]$", RegexOptions.Compiled);
    private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Z])(?=.*\d).{8,}$", RegexOptions.Compiled);

    // POST /api/auth/register
    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!UsernameRegex.IsMatch(request.Username))
            return BadRequest(new { message = "Kullanıcı adı 3-30 karakter olmalı, yalnızca harf, rakam, alt çizgi ve nokta içerebilir; başında veya sonunda nokta olamaz." });

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            return Conflict(new { message = "Bu kullanıcı adı zaten kullanılıyor." });

        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Bu e-posta adresi zaten kullanılıyor." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return StatusCode(201, new { user.Id, user.Username, user.Email, user.DisplayName });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var usernameLower = request.Username.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == usernameLower);

        if (user is null || user.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "Hesabınız askıya alınmış. Destek için yöneticinize başvurun." });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await StartSession(user);
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(new { message = "Oturum bulunamadı. Lütfen giriş yapın." });

        var result = await refreshTokens.RotateAsync(rawToken, HttpContext.RequestAborted);
        switch (result.Status)
        {
            case RefreshStatus.Success:
                WriteRefreshCookie(result.Token!);
                var user = result.User!;
                return Ok(new AuthResponse(jwtService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString())));

            case RefreshStatus.ConcurrentRotation:
                // Başka bir sekme aynı token'ı az önce yeniledi; tarayıcıdaki çerez güncellendi,
                // istemci tekrar denemeli. Çerez silinmez — yeni geçerli çerezi ezerdi.
                return Conflict(new { message = "Oturum başka bir sekmede yenilendi, tekrar deneyin." });

            case RefreshStatus.UserInactive:
                ClearRefreshCookie();
                return Unauthorized(new { message = "Hesabınız askıya alınmış." });

            default:
                ClearRefreshCookie();
                return Unauthorized(new { message = "Oturumun süresi doldu. Lütfen tekrar giriş yapın." });
        }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(rawToken))
            await refreshTokens.RevokeAsync(rawToken, HttpContext.RequestAborted);

        ClearRefreshCookie();
        return NoContent();
    }

    // GET /api/auth/me
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);

        if (user is null)
            return NotFound();

        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString()));
    }

    // POST /api/auth/google
    [HttpPost("google")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest request)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return StatusCode(503, new { message = "Google OAuth yapılandırılmamış." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Geçersiz Google token." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Google token doğrulaması başarısız.", detail = ex.Message });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject)
                ?? await db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (user is null)
        {
            var username = await GenerateUniqueUsernameAsync(payload.Email);
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = payload.Email,
                DisplayName = payload.Name,
                GoogleId = payload.Subject,
                EmailVerified = payload.EmailVerified,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }
        else if (user.GoogleId is null)
        {
            user.GoogleId = payload.Subject;
            user.UpdatedAt = DateTime.UtcNow;
        }

        if (!user.IsActive)
            return StatusCode(403, new { message = "Hesabınız askıya alınmış. Destek için yöneticinize başvurun." });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await StartSession(user);
    }

    // PUT /api/auth/profile  — display name ve/veya e-posta güncelle
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName.Trim();

        if (request.Email is not null)
        {
            var emailTrimmed = request.Email.Trim().ToLowerInvariant();
            if (emailTrimmed != user.Email &&
                await db.Users.AnyAsync(u => u.Email == emailTrimmed))
                return Conflict(new { message = "Bu e-posta adresi zaten kullanılıyor." });

            user.Email = emailTrimmed;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString()));
    }

    // POST /api/auth/change-password  — mevcut şifreyi doğrulayıp yeni şifre belirle
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (user.PasswordHash is null)
            return BadRequest(new { message = "Hesabınız Google ile oluşturulduğu için şifresi bulunmuyor." });

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Mevcut şifre hatalı." });

        if (!PasswordRegex.IsMatch(request.NewPassword))
            return BadRequest(new { message = "Yeni şifre en az 8 karakter, 1 büyük harf ve 1 rakam içermelidir." });

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "Yeni şifre mevcut şifreyle aynı olamaz." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Güvenlik: tüm mevcut oturumları sonlandır, bu oturum için yeni token ver
        await refreshTokens.RevokeAllAsync(userId);
        return await StartSession(user);
    }

    // DELETE /api/auth/account  — hesabı sil
    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(
        [FromServices] IStorageService storageService,
        [FromServices] XsltCraft.Infrastructure.Persistence.AppDbContext dbCtx)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kullanıcının asset dosyalarını storage'dan sil
        var assets = await db.Assets.Where(a => a.OwnerId == userId).ToListAsync();
        foreach (var asset in assets)
        {
            if (await storageService.ExistsAsync(asset.FilePath))
                await storageService.DeleteAsync(asset.FilePath);
        }

        // Kullanıcının template XSLT cache dosyalarını storage'dan sil, template'leri DB'den sil
        var templates = await db.Templates
            .Where(t => t.OwnerId == userId && !t.IsFreeTheme)
            .ToListAsync();
        foreach (var tpl in templates)
        {
            if (!string.IsNullOrEmpty(tpl.XsltStoragePath)
                && await storageService.ExistsAsync(tpl.XsltStoragePath))
                await storageService.DeleteAsync(tpl.XsltStoragePath);
        }
        db.Templates.RemoveRange(templates);

        // Kullanıcıyı sil — Assets cascade (DB), RefreshTokens cascade (DB)
        var user = await db.Users.FindAsync(userId);
        if (user is not null) db.Users.Remove(user);

        await db.SaveChangesAsync();

        ClearRefreshCookie();
        return NoContent();
    }

    // --- Helpers ---

    private async Task<IActionResult> StartSession(User user)
    {
        var issued = await refreshTokens.IssueAsync(user.Id, HttpContext.RequestAborted);
        WriteRefreshCookie(issued);
        return Ok(new AuthResponse(jwtService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString())));
    }

    private void WriteRefreshCookie(IssuedRefreshToken token)
    {
        DeleteLegacyRefreshCookie();
        Response.Cookies.Append(RefreshTokenCookie, token.RawToken, RefreshCookieOptions(token.ExpiresAt));
    }

    private void ClearRefreshCookie()
    {
        DeleteLegacyRefreshCookie();
        Response.Cookies.Delete(RefreshTokenCookie, RefreshCookieOptions(expires: null));
    }

    // 1.10.0 ve öncesi çerezi Path=/ ile yazıyordu; aynı adlı eski çerez kalmasın.
    private void DeleteLegacyRefreshCookie() => Response.Cookies.Delete(RefreshTokenCookie);

    private CookieOptions RefreshCookieOptions(DateTime? expires)
    {
        var isHttps = Request.IsHttps;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.Strict : SameSiteMode.Lax,
            Path = RefreshTokenCookiePath,
            Expires = expires,
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(string email)
    {
        var prefix = email.Split('@')[0].ToLowerInvariant();
        var cleaned = Regex.Replace(prefix, "[^a-z0-9_]", "");
        if (cleaned.Length < 3) cleaned = cleaned.PadRight(3, '0');
        if (cleaned.Length > 20) cleaned = cleaned[..20];

        if (!await db.Users.AnyAsync(u => u.Username.ToLower() == cleaned))
            return cleaned;

        for (int i = 1; i <= 999; i++)
        {
            var candidate = $"{cleaned}_{i}";
            if (!await db.Users.AnyAsync(u => u.Username.ToLower() == candidate))
                return candidate;
        }

        return $"{cleaned}_{Guid.NewGuid().ToString("N")[..6]}";
    }
}
