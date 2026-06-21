using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Services;

public class EntitlementService(AppDbContext db, IOptions<MembershipOptions> options) : IEntitlementService
{
    public async Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Role, u.Plan, u.PlanExpiresAt })
            .FirstOrDefaultAsync(ct);

        // Bilinmeyen kullanıcı → en kısıtlı (User + Free) gibi davran.
        if (user is null)
            return EntitlementPolicy.Compute(UserRole.User, MembershipPlan.Free, null, options.Value, DateTime.UtcNow);

        return EntitlementPolicy.Compute(user.Role, user.Plan, user.PlanExpiresAt, options.Value, DateTime.UtcNow);
    }
}
