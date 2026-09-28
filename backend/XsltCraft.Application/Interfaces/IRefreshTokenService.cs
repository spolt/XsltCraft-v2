using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces;

public sealed record IssuedRefreshToken(string RawToken, DateTime ExpiresAt);

public enum RefreshStatus
{
    Success,
    /// <summary>Token yok, süresi dolmuş veya iptal edilmiş.</summary>
    Invalid,
    /// <summary>Döndürülmüş token tekrar kullanıldı; kullanıcının tüm oturumları iptal edildi.</summary>
    ReuseDetected,
    /// <summary>Başka bir istek bu token'ı az önce döndürdü; istemci güncel çerezle tekrar denemeli.</summary>
    ConcurrentRotation,
    UserInactive,
}

public sealed record RefreshResult(RefreshStatus Status, IssuedRefreshToken? Token = null, User? User = null);

/// <summary>Refresh token yaşam döngüsü: üretim, döndürme (rotation), iptal.</summary>
public interface IRefreshTokenService
{
    /// <summary>Yeni oturum açar (login / şifre değişikliği).</summary>
    Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Token'ı doğrular, eskisini iptal edip aynı oturumda yenisini verir.</summary>
    Task<RefreshResult> RotateAsync(string rawToken, CancellationToken ct = default);

    Task RevokeAsync(string rawToken, CancellationToken ct = default);

    Task RevokeAllAsync(Guid userId, CancellationToken ct = default);
}
