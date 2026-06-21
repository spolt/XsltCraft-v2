using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Membership;

/// <summary>
/// Rol + plan + abonelik geçerliliğinden yetkileri hesaplayan saf (DB'siz) politika.
/// DB erişimi olmadığı için doğrudan birim testi yazılabilir.
/// </summary>
public static class EntitlementPolicy
{
    /// <summary>
    /// Etkin plan: Pro yalnızca süresi geçmemişse Pro'dur; aksi halde Free'ye düşer.
    /// (Editör/Admin rolleri için plan anlamsızdır; <see cref="Compute"/> bunları zaten bypass eder.)
    /// </summary>
    public static MembershipPlan EffectivePlan(MembershipPlan plan, DateTime? planExpiresAt, DateTime utcNow)
        => plan == MembershipPlan.Pro && (planExpiresAt is null || planExpiresAt > utcNow)
            ? MembershipPlan.Pro
            : MembershipPlan.Free;

    public static UserEntitlements Compute(
        UserRole role,
        MembershipPlan plan,
        DateTime? planExpiresAt,
        MembershipOptions options,
        DateTime utcNow)
    {
        // Editör ve Admin: tüm gate'leri bypass eder, kotasız. (Editör'den admin paneli yine de gizli — o ayrı.)
        if (role is UserRole.Editor or UserRole.Admin)
        {
            return new UserEntitlements(
                Role: role,
                EffectivePlan: MembershipPlan.Pro,   // raporlama için; etkin olarak sınırsız
                IsPrivileged: true,
                CanDownloadGridXslt: true,
                CanSaveRawXslt: true,
                CanAccessRawXslt: true,
                CanUsePremiumThemes: true,
                DailyAiRequestLimit: 0,
                DailyAiTokenLimit: 0,
                DailyTemplateExportLimit: 0);
        }

        var effectivePlan = EffectivePlan(plan, planExpiresAt, utcNow);
        var limits = effectivePlan == MembershipPlan.Pro ? options.Pro : options.Free;

        return new UserEntitlements(
            Role: role,
            EffectivePlan: effectivePlan,
            IsPrivileged: false,
            CanDownloadGridXslt: limits.CanDownloadGridXslt,
            CanSaveRawXslt: limits.CanSaveRawXslt,
            CanAccessRawXslt: limits.CanAccessRawXslt,
            CanUsePremiumThemes: limits.CanUsePremiumThemes,
            DailyAiRequestLimit: limits.DailyAiRequestLimit,
            DailyAiTokenLimit: limits.DailyAiTokenLimit,
            DailyTemplateExportLimit: limits.DailyTemplateExportLimit);
    }
}
