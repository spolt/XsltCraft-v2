namespace XsltCraft.Application.Auth;

/// <summary>
/// Oturum ömrü politikası. appsettings "Session" bölümünden override edilebilir.
/// Access token ömrü ayrıca <c>Jwt:AccessTokenExpiryMinutes</c> ile yönetilir (15 dk).
/// </summary>
public class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>Hareketsizlik süresi: bu süre içinde refresh yapılmazsa oturum düşer (kayan pencere).</summary>
    public int IdleTimeoutMinutes { get; set; } = 120;

    /// <summary>Mutlak oturum ömrü: aktif kullanılsa da login'den bu kadar sonra yeniden giriş zorunludur.</summary>
    public int AbsoluteLifetimeHours { get; set; } = 12;

    /// <summary>
    /// Yeni döndürülmüş bir token'ın eski hâli bu süre içinde tekrar gelirse token hırsızlığı değil,
    /// eşzamanlı yenileme (iki sekme) kabul edilir.
    /// </summary>
    public int RotationGraceSeconds { get; set; } = 30;
}
