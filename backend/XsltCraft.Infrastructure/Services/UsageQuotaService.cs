using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Services;

/// <summary>
/// Günlük kota sayaçlarını (AI istek/token + şablon indirme) tek <c>UserAiUsages</c> satırında
/// (UserId, Date) tutar. UTC gece yarısı otomatik sıfırlanır (yeni gün = yeni satır).
/// Plan-bilinçli kontroller için <see cref="IEntitlementService"/> kullanır.
/// </summary>
public class UsageQuotaService(AppDbContext db, IEntitlementService entitlements) : IUsageQuotaService
{
    public async Task<QuotaCheck> CheckAiAllowedAsync(Guid userId, CancellationToken ct = default)
    {
        var ent = await entitlements.GetAsync(userId, ct);
        if (ent.IsPrivileged) return new QuotaCheck(true);

        var usage = await GetTodayAsync(userId, ct);

        // Free: günlük istek (soru) limiti — Pro'ya yönlendiren neden.
        if (ent.DailyAiRequestLimit > 0 && usage.AiRequestCount >= ent.DailyAiRequestLimit)
            return new QuotaCheck(false, QuotaDenyReason.AiRequestLimit);

        // Pro: günlük token bütçesi.
        if (ent.DailyAiTokenLimit > 0 && usage.AiTokensUsed >= ent.DailyAiTokenLimit)
            return new QuotaCheck(false, QuotaDenyReason.AiTokenBudget);

        return new QuotaCheck(true);
    }

    public Task IncrementAiAsync(Guid userId, int approximateTokens, CancellationToken ct = default)
        => BumpAsync(userId, tokenDelta: Math.Max(0, approximateTokens), aiRequestDelta: 1, exportDelta: 0, ct);

    public async Task<QuotaCheck> CheckTemplateExportAsync(Guid userId, CancellationToken ct = default)
    {
        var ent = await entitlements.GetAsync(userId, ct);
        if (ent.IsPrivileged) return new QuotaCheck(true);

        if (!ent.CanDownloadGridXslt)
            return new QuotaCheck(false, QuotaDenyReason.ExportNotAllowed);

        var usage = await GetTodayAsync(userId, ct);
        if (ent.DailyTemplateExportLimit > 0 && usage.TemplateExportCount >= ent.DailyTemplateExportLimit)
            return new QuotaCheck(false, QuotaDenyReason.ExportDailyLimit);

        return new QuotaCheck(true);
    }

    public Task IncrementTemplateExportAsync(Guid userId, CancellationToken ct = default)
        => BumpAsync(userId, tokenDelta: 0, aiRequestDelta: 0, exportDelta: 1, ct);

    public async Task<DailyUsage> GetUsageAsync(Guid userId, CancellationToken ct = default)
        => await GetTodayAsync(userId, ct);

    public async Task<IReadOnlyList<UserAiUsageSummary>> GetDailyUsageAsync(DateOnly date, CancellationToken ct = default)
    {
        return await db.UserAiUsages
            .Where(u => u.Date == date)
            .Join(db.Users,
                u => u.UserId,
                user => user.Id,
                (u, user) => new UserAiUsageSummary(u.UserId, user.Username, user.Email, u.TokensUsed))
            .OrderByDescending(s => s.TokensUsed)
            .ToListAsync(ct);
    }

    private async Task<DailyUsage> GetTodayAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await db.UserAiUsages
            .Where(u => u.UserId == userId && u.Date == today)
            .Select(u => new DailyUsage(u.AiRequestCount, u.TokensUsed, u.TemplateExportCount))
            .FirstOrDefaultAsync(ct);

        return row ?? new DailyUsage(0, 0, 0);
    }

    /// <summary>Bugünkü satırı atomik artırır; satır yoksa oluşturur (eşzamanlılık yarışına dayanıklı).</summary>
    private async Task BumpAsync(Guid userId, int tokenDelta, int aiRequestDelta, int exportDelta, CancellationToken ct)
    {
        if (tokenDelta == 0 && aiRequestDelta == 0 && exportDelta == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var updated = await db.UserAiUsages
            .Where(u => u.UserId == userId && u.Date == today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.TokensUsed, u => u.TokensUsed + tokenDelta)
                .SetProperty(u => u.AiRequestCount, u => u.AiRequestCount + aiRequestDelta)
                .SetProperty(u => u.TemplateExportCount, u => u.TemplateExportCount + exportDelta)
                .SetProperty(u => u.UpdatedAt, _ => DateTime.UtcNow), ct);

        if (updated != 0) return;

        var entry = db.UserAiUsages.Add(new UserAiUsage
        {
            UserId = userId,
            Date = today,
            TokensUsed = tokenDelta,
            AiRequestCount = aiRequestDelta,
            TemplateExportCount = exportDelta,
            UpdatedAt = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Yarış: başka bir eşzamanlı istek satırı eklemiş — sadece güncelle.
            entry.State = EntityState.Detached;
            await db.UserAiUsages
                .Where(u => u.UserId == userId && u.Date == today)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.TokensUsed, u => u.TokensUsed + tokenDelta)
                    .SetProperty(u => u.AiRequestCount, u => u.AiRequestCount + aiRequestDelta)
                    .SetProperty(u => u.TemplateExportCount, u => u.TemplateExportCount + exportDelta)
                    .SetProperty(u => u.UpdatedAt, _ => DateTime.UtcNow), ct);
        }
    }
}
