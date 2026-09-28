namespace XsltCraft.Application.Auth;

/// <summary>Bir refresh token'ın sunulduğu andaki durumu (DB'den bağımsız).</summary>
public readonly record struct RefreshTokenState(
    DateTime ExpiresAt,
    DateTime SessionExpiresAt,
    DateTime? RevokedAt,
    bool WasRotated);

public enum RefreshDecision
{
    /// <summary>Token geçerli: döndür ve yeni token ver.</summary>
    Rotate,
    /// <summary>Hareketsizlik veya mutlak süre dolmuş ya da token iptal edilmiş.</summary>
    Expired,
    /// <summary>Döndürülmüş token grace süresinden sonra tekrar kullanıldı — çalınmış kabul edilir.</summary>
    ReuseDetected,
    /// <summary>Döndürülmüş token grace süresi içinde tekrar geldi — başka sekme az önce yeniledi.</summary>
    ConcurrentRotation,
}

/// <summary>
/// Oturum ömrü kuralları: hareketsizlik (kayan) + mutlak süre + rotation reuse tespiti.
/// Saf mantık; zaman dışarıdan verilir.
/// </summary>
public sealed class SessionPolicy(SessionOptions options)
{
    private TimeSpan IdleTimeout => TimeSpan.FromMinutes(options.IdleTimeoutMinutes);
    private TimeSpan AbsoluteLifetime => TimeSpan.FromHours(options.AbsoluteLifetimeHours);
    private TimeSpan RotationGrace => TimeSpan.FromSeconds(options.RotationGraceSeconds);

    /// <summary>Login anında açılan oturumun token ve mutlak bitiş zamanları.</summary>
    public (DateTime ExpiresAt, DateTime SessionExpiresAt) StartSession(DateTime now)
    {
        var sessionExpiresAt = now + AbsoluteLifetime;
        return (NextExpiry(now, sessionExpiresAt), sessionExpiresAt);
    }

    /// <summary>Yenilemede verilecek token'ın bitişi: hareketsizlik penceresi, mutlak sınırı aşamaz.</summary>
    public DateTime NextExpiry(DateTime now, DateTime sessionExpiresAt)
    {
        var idleExpiry = now + IdleTimeout;
        return idleExpiry < sessionExpiresAt ? idleExpiry : sessionExpiresAt;
    }

    public RefreshDecision Evaluate(RefreshTokenState token, DateTime now)
    {
        if (token.RevokedAt is { } revokedAt)
        {
            // Yalnız döndürülmüş token'ın tekrar kullanımı hırsızlık sinyalidir; logout/şifre
            // değişikliğiyle iptal edilmiş token sadece geçersizdir.
            if (!token.WasRotated) return RefreshDecision.Expired;
            return now - revokedAt <= RotationGrace
                ? RefreshDecision.ConcurrentRotation
                : RefreshDecision.ReuseDetected;
        }

        if (now >= token.ExpiresAt || now >= token.SessionExpiresAt)
            return RefreshDecision.Expired;

        return RefreshDecision.Rotate;
    }
}
