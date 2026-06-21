using XsltCraft.Application.Membership;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.Membership;

/// <summary>
/// Üyelik yetki matrisinin saf (DB'siz) doğrulaması: Free/Pro/Editör/Admin × yetenekler,
/// abonelik süresi dolması ve rol-bypass davranışı.
/// </summary>
public class EntitlementPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
    private static MembershipOptions Options => new(); // appsettings varsayılanlarıyla aynı

    private static UserEntitlements Compute(UserRole role, MembershipPlan plan, DateTime? expires = null)
        => EntitlementPolicy.Compute(role, plan, expires, Options, Now);

    [Fact]
    public void Free_user_is_fully_restricted_with_one_ai_question_per_day()
    {
        var e = Compute(UserRole.User, MembershipPlan.Free);

        Assert.Equal(MembershipPlan.Free, e.EffectivePlan);
        Assert.False(e.IsPrivileged);
        Assert.False(e.CanDownloadGridXslt);
        Assert.False(e.CanSaveRawXslt);
        Assert.False(e.CanAccessRawXslt);
        Assert.False(e.CanUsePremiumThemes);
        Assert.Equal(1, e.DailyAiRequestLimit);     // günde 1 soru
        Assert.Equal(0, e.DailyAiTokenLimit);        // 0 = sınırsız (tek soru zaten sınırlı)
        Assert.Equal(0, e.DailyTemplateExportLimit); // indirme yok (CanDownloadGridXslt=false)
    }

    [Fact]
    public void Pro_user_has_full_access_with_daily_caps()
    {
        var e = Compute(UserRole.User, MembershipPlan.Pro);

        Assert.Equal(MembershipPlan.Pro, e.EffectivePlan);
        Assert.False(e.IsPrivileged);
        Assert.True(e.CanDownloadGridXslt);
        Assert.True(e.CanSaveRawXslt);
        Assert.True(e.CanAccessRawXslt);
        Assert.True(e.CanUsePremiumThemes);
        Assert.Equal(0, e.DailyAiRequestLimit);       // sınırsız istek (token bütçesiyle yönetilir)
        Assert.Equal(50_000, e.DailyAiTokenLimit);    // 50k token/gün
        Assert.Equal(3, e.DailyTemplateExportLimit);  // 3 indirme/gün
    }

    [Fact]
    public void Pro_with_future_expiry_stays_pro()
    {
        var e = Compute(UserRole.User, MembershipPlan.Pro, Now.AddDays(5));

        Assert.Equal(MembershipPlan.Pro, e.EffectivePlan);
        Assert.True(e.CanDownloadGridXslt);
        Assert.Equal(3, e.DailyTemplateExportLimit);
    }

    [Fact]
    public void Pro_with_past_expiry_downgrades_to_free()
    {
        var e = Compute(UserRole.User, MembershipPlan.Pro, Now.AddDays(-1));

        Assert.Equal(MembershipPlan.Free, e.EffectivePlan);
        Assert.False(e.CanDownloadGridXslt);
        Assert.False(e.CanSaveRawXslt);
        Assert.Equal(1, e.DailyAiRequestLimit);
    }

    [Fact]
    public void Pro_expiring_exactly_now_is_treated_as_expired()
    {
        // expiresAt <= now → Free
        var e = Compute(UserRole.User, MembershipPlan.Pro, Now);

        Assert.Equal(MembershipPlan.Free, e.EffectivePlan);
        Assert.False(e.CanDownloadGridXslt);
    }

    [Theory]
    [InlineData(UserRole.Editor)]
    [InlineData(UserRole.Admin)]
    public void Privileged_roles_bypass_all_quotas_regardless_of_plan(UserRole role)
    {
        // Free planlı olsa bile rol bypass eder.
        var e = Compute(role, MembershipPlan.Free, Now.AddDays(-100));

        Assert.True(e.IsPrivileged);
        Assert.True(e.CanDownloadGridXslt);
        Assert.True(e.CanSaveRawXslt);
        Assert.True(e.CanAccessRawXslt);
        Assert.True(e.CanUsePremiumThemes);
        Assert.Equal(0, e.DailyAiRequestLimit);       // 0 = sınırsız
        Assert.Equal(0, e.DailyAiTokenLimit);
        Assert.Equal(0, e.DailyTemplateExportLimit);
    }

    [Theory]
    [InlineData(MembershipPlan.Free, null, MembershipPlan.Free)]
    [InlineData(MembershipPlan.Pro, null, MembershipPlan.Pro)]
    public void EffectivePlan_resolves_correctly(MembershipPlan stored, DateTime? expires, MembershipPlan expected)
    {
        Assert.Equal(expected, EntitlementPolicy.EffectivePlan(stored, expires, Now));
    }

    [Fact]
    public void Options_override_changes_limits()
    {
        var custom = new MembershipOptions();
        custom.Pro.DailyTemplateExportLimit = 10;
        custom.Free.DailyAiRequestLimit = 2;

        var pro = EntitlementPolicy.Compute(UserRole.User, MembershipPlan.Pro, null, custom, Now);
        var free = EntitlementPolicy.Compute(UserRole.User, MembershipPlan.Free, null, custom, Now);

        Assert.Equal(10, pro.DailyTemplateExportLimit);
        Assert.Equal(2, free.DailyAiRequestLimit);
    }
}
