using System.Security.Claims;

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
public class AuthController(AppDbContext db, IJwtService jwtService, IConfiguration configuration) : ControllerBase
{
    private const string RefreshTokenCookie = "refreshToken";
    private static readonly TimeSpan RefreshTokenExpiry = TimeSpan.FromDays(30);

    // POST /api/auth/register
    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Bu e-posta adresi zaten kullanılıyor." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return StatusCode(201, new { user.Id, user.Email, user.DisplayName });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || user.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });

        var accessToken = jwtService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        await SetNewRefreshToken(user.Id);

        return Ok(new AuthResponse(accessToken));
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(new { message = "Refresh token bulunamadı." });

        var existing = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == rawToken);

        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt < DateTime.UtcNow)
        {
            // Compromise detection: if expired/revoked token used, revoke all tokens for safety
            if (existing is not null)
                await RevokeAllUserTokens(existing.UserId);

            return Unauthorized(new { message = "Geçersiz veya süresi dolmuş refresh token." });
        }

        // Rotate: revoke old, issue new
        existing.RevokedAt = DateTime.UtcNow;
        var accessToken = jwtService.GenerateAccessToken(
            existing.UserId, existing.User.Email, existing.User.Role.ToString());
        await SetNewRefreshToken(existing.UserId);

        return Ok(new AuthResponse(accessToken));
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(rawToken))
        {
            var token = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == rawToken);
            if (token is not null && token.RevokedAt is null)
                token.RevokedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }

        Response.Cookies.Delete(RefreshTokenCookie);
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
            user = new User
            {
                Id = Guid.NewGuid(),
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

        await db.SaveChangesAsync();

        var accessToken = jwtService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        await SetNewRefreshToken(user.Id);

        return Ok(new AuthResponse(accessToken));
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

        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    // --- Helpers ---

    private async Task SetNewRefreshToken(Guid userId)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = jwtService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenExpiry),
            CreatedAt = DateTime.UtcNow
        };

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        var isHttps = Request.IsHttps;
        Response.Cookies.Append(RefreshTokenCookie, token.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.Strict : SameSiteMode.Lax,
            Expires = token.ExpiresAt
        });
    }

    private async Task RevokeAllUserTokens(Guid userId)
    {
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
