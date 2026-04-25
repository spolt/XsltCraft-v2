using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Services;

public class UserManagementService(AppDbContext db) : IUserManagementService
{
    public async Task<UserListResponse> ListUsersAsync(string? query, string? role, bool? isActive, int page, int pageSize)
    {
        var q = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLowerInvariant();
            q = q.Where(u => u.Email.ToLower().Contains(lower) ||
                             (u.DisplayName != null && u.DisplayName.ToLower().Contains(lower)));
        }

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
            q = q.Where(u => u.Role == roleEnum);

        if (isActive.HasValue)
            q = q.Where(u => u.IsActive == isActive.Value);

        var totalCount = await q.CountAsync();

        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        var activityStats = await db.UserActivities
            .Where(a => userIds.Contains(a.UserId))
            .GroupBy(a => new { a.UserId, a.Type })
            .Select(g => new { g.Key.UserId, g.Key.Type, Count = g.Count() })
            .ToListAsync();

        var lastActivity = await db.UserActivities
            .Where(a => userIds.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, LastAt = g.Max(a => a.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.LastAt);

        var items = users.Select(u =>
        {
            var saveCount = activityStats
                .FirstOrDefault(a => a.UserId == u.Id && a.Type == UserActivityType.Save)?.Count ?? 0;
            var downloadCount = activityStats
                .FirstOrDefault(a => a.UserId == u.Id && a.Type == UserActivityType.Download)?.Count ?? 0;
            lastActivity.TryGetValue(u.Id, out var lastAt);

            return new UserListItem(
                u.Id, u.Username, u.Email, u.DisplayName, u.Role.ToString(),
                u.IsActive, saveCount, downloadCount,
                lastAt == default ? null : lastAt,
                u.LastLoginAt, u.CreatedAt);
        }).ToList();

        var activeCount = await db.Users.CountAsync(u => u.IsActive);
        var inactiveCount = await db.Users.CountAsync(u => !u.IsActive);
        var totalActivityCount = await db.UserActivities.CountAsync();

        return new UserListResponse(items, totalCount, page, pageSize, activeCount, inactiveCount, totalActivityCount);
    }

    public async Task<UserListItem?> GetUserAsync(Guid id)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return null;

        var saveCount = await db.UserActivities
            .CountAsync(a => a.UserId == id && a.Type == UserActivityType.Save);
        var downloadCount = await db.UserActivities
            .CountAsync(a => a.UserId == id && a.Type == UserActivityType.Download);
        var lastAt = await db.UserActivities
            .Where(a => a.UserId == id)
            .MaxAsync(a => (DateTime?)a.CreatedAt);

        return new UserListItem(
            u.Id, u.Username, u.Email, u.DisplayName, u.Role.ToString(),
            u.IsActive, saveCount, downloadCount, lastAt, u.LastLoginAt, u.CreatedAt);
    }

    public async Task<(bool Success, string? Error)> SetRoleAsync(Guid targetUserId, Guid adminId, string role)
    {
        if (targetUserId == adminId)
            return (false, "Kendi rolünüzü değiştiremezsiniz.");

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
            return (false, "Geçersiz rol. Geçerli değerler: User, Editor, Admin");

        var user = await db.Users.FindAsync(targetUserId);
        if (user is null) return (false, "Kullanıcı bulunamadı.");

        user.Role = roleEnum;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetActiveAsync(Guid targetUserId, Guid adminId, bool isActive)
    {
        if (targetUserId == adminId)
            return (false, "Kendi hesabınızı pasif yapamazsınız.");

        var user = await db.Users.FindAsync(targetUserId);
        if (user is null) return (false, "Kullanıcı bulunamadı.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Pasife alırken refresh token'ları revoke et
        if (!isActive)
        {
            var tokens = await db.RefreshTokens
                .Where(rt => rt.UserId == targetUserId && rt.RevokedAt == null)
                .ToListAsync();
            foreach (var t in tokens)
                t.RevokedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(CreateManagedUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return (false, "Kullanıcı adı en az 3 karakter olmalıdır.");

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            return (false, "Bu kullanıcı adı zaten kullanılıyor.");

        var emailLower = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == emailLower))
            return (false, "Bu e-posta adresi zaten kullanılıyor.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var roleEnum))
            return (false, "Geçersiz rol. Geçerli değerler: User, Editor, Admin");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = emailLower,
            DisplayName = request.DisplayName?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = roleEnum,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(Guid targetUserId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.");

        var user = await db.Users.FindAsync(targetUserId);
        if (user is null) return (false, "Kullanıcı bulunamadı.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Mevcut refresh token'ları revoke et
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == targetUserId && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(Guid targetUserId, Guid adminId)
    {
        if (targetUserId == adminId)
            return (false, "Kendi hesabınızı silemezsiniz.");

        var user = await db.Users.FindAsync(targetUserId);
        if (user is null) return (false, "Kullanıcı bulunamadı.");

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return (true, null);
    }
}
