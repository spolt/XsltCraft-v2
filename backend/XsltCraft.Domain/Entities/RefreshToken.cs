namespace XsltCraft.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Ham token'ın SHA-256 özeti (hex). Ham değer yalnız HttpOnly çerezde bulunur.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Hareketsizlik bitişi: her yenilemede ileri kayar, <see cref="SessionExpiresAt"/>'i aşamaz.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Mutlak oturum bitişi: login'de belirlenir, rotation'larda aynen taşınır.</summary>
    public DateTime SessionExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Rotation ile iptal edildiyse yerine verilen token. Reuse tespiti için kullanılır.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
