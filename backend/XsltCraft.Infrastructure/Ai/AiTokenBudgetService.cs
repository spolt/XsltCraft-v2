using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Ai;

public interface IAiTokenBudgetService
{
    Task<bool> IsWithinBudgetAsync(Guid userId, CancellationToken ct = default);
    Task IncrementAsync(Guid userId, int approximateTokens, CancellationToken ct = default);
    Task<IReadOnlyList<UserAiUsageSummary>> GetDailyUsageAsync(DateOnly date, CancellationToken ct = default);
}

public record UserAiUsageSummary(Guid UserId, string? Username, string Email, int TokensUsed);

public class AiTokenBudgetService : IAiTokenBudgetService
{
    private readonly AppDbContext _db;
    private readonly IOptions<AiOptions> _options;

    public AiTokenBudgetService(AppDbContext db, IOptions<AiOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<bool> IsWithinBudgetAsync(Guid userId, CancellationToken ct = default)
    {
        var limit = _options.Value.DailyTokenBudgetPerUser;
        if (limit <= 0) return true;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var used = await _db.UserAiUsages
            .Where(u => u.UserId == userId && u.Date == today)
            .Select(u => u.TokensUsed)
            .FirstOrDefaultAsync(ct);

        return used < limit;
    }

    public async Task IncrementAsync(Guid userId, int approximateTokens, CancellationToken ct = default)
    {
        if (approximateTokens <= 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var updated = await _db.UserAiUsages
            .Where(u => u.UserId == userId && u.Date == today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.TokensUsed, u => u.TokensUsed + approximateTokens)
                .SetProperty(u => u.UpdatedAt, _ => DateTime.UtcNow), ct);

        if (updated == 0)
        {
            var entry = _db.UserAiUsages.Add(new UserAiUsage
            {
                UserId = userId,
                Date = today,
                TokensUsed = approximateTokens,
                UpdatedAt = DateTime.UtcNow,
            });
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race: another concurrent request inserted the row — just update it.
                entry.State = EntityState.Detached;
                await _db.UserAiUsages
                    .Where(u => u.UserId == userId && u.Date == today)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.TokensUsed, u => u.TokensUsed + approximateTokens)
                        .SetProperty(u => u.UpdatedAt, _ => DateTime.UtcNow), ct);
            }
        }
    }

    public async Task<IReadOnlyList<UserAiUsageSummary>> GetDailyUsageAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _db.UserAiUsages
            .Where(u => u.Date == date)
            .Join(_db.Users,
                u => u.UserId,
                user => user.Id,
                (u, user) => new UserAiUsageSummary(u.UserId, user.Username, user.Email, u.TokensUsed))
            .OrderByDescending(s => s.TokensUsed)
            .ToListAsync(ct);
    }
}
