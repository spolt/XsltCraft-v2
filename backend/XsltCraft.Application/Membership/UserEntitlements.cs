using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Membership;

/// <summary>
/// Bir kullanıcının etkin yetkileri: rol + plan + abonelik geçerliliğinden hesaplanır.
/// Gate'ler (indirme/kaydetme/AI/ücretli tema) ve frontend rozetleri bunu kullanır.
/// Limitlerde <c>0 = sınırsız</c>. <see cref="IsPrivileged"/> true ise tüm kotalar geçersizdir.
/// </summary>
public record UserEntitlements(
    UserRole Role,
    MembershipPlan EffectivePlan,
    bool IsPrivileged,
    bool CanDownloadGridXslt,
    bool CanSaveRawXslt,
    bool CanAccessRawXslt,
    bool CanUsePremiumThemes,
    int DailyAiRequestLimit,
    int DailyAiTokenLimit,
    int DailyTemplateExportLimit);
