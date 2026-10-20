using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Themes;

public enum ThemeAccessDecision { Allowed, NotFound, UpgradeRequired }

/// <summary>
/// Hazır temanın saklı XSLT'sine erişim kuralı — tek kaynak: yalnız admin'in yayınladığı temalar
/// (<c>IsFreeTheme</c>) bu yoldan okunabilir; ücretli tema Pro/Editör/Admin yetkisi (<c>CanUsePremiumThemes</c>) ister.
/// Kullanıcı şablonlarının saklı XSLT'si bu yoldan sızmaz.
/// </summary>
public interface IThemeAccessGuard
{
    Task<ThemeAccessDecision> CheckAsync(Template theme, Guid? userId, CancellationToken ct = default);
}

public sealed class ThemeAccessGuard(IEntitlementService entitlements) : IThemeAccessGuard
{
    public const string UpgradeMessage = "Bu ücretli tema XsltCraft Pro üyeliği gerektirir.";

    public async Task<ThemeAccessDecision> CheckAsync(Template theme, Guid? userId, CancellationToken ct = default)
    {
        if (!theme.IsFreeTheme || string.IsNullOrEmpty(theme.XsltStoragePath))
            return ThemeAccessDecision.NotFound;

        if (!theme.IsPremium)
            return ThemeAccessDecision.Allowed;

        if (userId is null)
            return ThemeAccessDecision.UpgradeRequired;

        var ent = await entitlements.GetAsync(userId.Value, ct);
        return ent.CanUsePremiumThemes ? ThemeAccessDecision.Allowed : ThemeAccessDecision.UpgradeRequired;
    }
}
