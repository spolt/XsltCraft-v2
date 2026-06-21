using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Services;

public class UsageReportService(AppDbContext db) : IUsageReportService
{
    public async Task<UsageReportResponse> GetReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // AI/indirme sayaçları: günlük UserAiUsages satırlarını kullanıcı başına topla.
        var aiAgg = await db.UserAiUsages
            .Where(u => u.Date >= from && u.Date <= to)
            .GroupBy(u => u.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Tokens = g.Sum(x => (long)x.TokensUsed),
                AiRequests = g.Sum(x => x.AiRequestCount),
                Exports = g.Sum(x => x.TemplateExportCount),
            })
            .ToListAsync(ct);

        // Kaydetme/indirme aktiviteleri: UserActivities olaylarını kullanıcı + tip bazında say.
        var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDtExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var actAgg = await db.UserActivities
            .Where(a => a.CreatedAt >= fromDt && a.CreatedAt < toDtExclusive)
            .GroupBy(a => new { a.UserId, a.Type })
            .Select(g => new { g.Key.UserId, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);

        var userIds = aiAgg.Select(a => a.UserId)
            .Concat(actAgg.Select(a => a.UserId))
            .Distinct()
            .ToList();

        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.Email })
            .ToListAsync(ct);

        var rows = users.Select(u =>
        {
            var ai = aiAgg.FirstOrDefault(a => a.UserId == u.Id);
            var saves = actAgg.FirstOrDefault(a => a.UserId == u.Id && a.Type == UserActivityType.Save)?.Count ?? 0;
            var downloads = actAgg.FirstOrDefault(a => a.UserId == u.Id && a.Type == UserActivityType.Download)?.Count ?? 0;
            return new UsageReportRow(
                u.Id, u.Username, u.Email,
                ai?.Tokens ?? 0, ai?.AiRequests ?? 0, ai?.Exports ?? 0,
                saves, downloads);
        })
        .OrderByDescending(r => r.TokensUsed)
        .ThenByDescending(r => r.DownloadCount)
        .ToList();

        var totals = new UsageReportTotals(
            rows.Sum(r => r.TokensUsed),
            rows.Sum(r => r.AiRequests),
            rows.Sum(r => r.TemplateExports),
            rows.Sum(r => r.SaveCount),
            rows.Sum(r => r.DownloadCount),
            rows.Count);

        return new UsageReportResponse(from, to, rows, totals);
    }

    public async Task<IReadOnlyList<UsageDailyPoint>> GetDailyTotalsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var aiByDate = await db.UserAiUsages
            .Where(u => u.Date >= from && u.Date <= to)
            .GroupBy(u => u.Date)
            .Select(g => new
            {
                Date = g.Key,
                Tokens = g.Sum(x => (long)x.TokensUsed),
                AiRequests = g.Sum(x => x.AiRequestCount),
                Exports = g.Sum(x => x.TemplateExportCount),
            })
            .ToListAsync(ct);

        var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDtExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // CreatedAt (DateTime, UTC) → gün; Npgsql .Date çevirisini destekler.
        var actByDate = await db.UserActivities
            .Where(a => a.CreatedAt >= fromDt && a.CreatedAt < toDtExclusive)
            .GroupBy(a => new { Day = a.CreatedAt.Date, a.Type })
            .Select(g => new { g.Key.Day, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);

        var dates = aiByDate.Select(a => a.Date)
            .Concat(actByDate.Select(a => DateOnly.FromDateTime(a.Day)))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return dates.Select(d =>
        {
            var ai = aiByDate.FirstOrDefault(a => a.Date == d);
            var saves = actByDate.FirstOrDefault(a => DateOnly.FromDateTime(a.Day) == d && a.Type == UserActivityType.Save)?.Count ?? 0;
            var downloads = actByDate.FirstOrDefault(a => DateOnly.FromDateTime(a.Day) == d && a.Type == UserActivityType.Download)?.Count ?? 0;
            return new UsageDailyPoint(
                d, ai?.Tokens ?? 0, ai?.AiRequests ?? 0, ai?.Exports ?? 0, saves, downloads);
        }).ToList();
    }
}
