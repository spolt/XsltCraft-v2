namespace XsltCraft.Application.Interfaces;

/// <summary>
/// Admin kullanım raporu: kullanıcı başına token/AI-isteği/indirme-export ve kaydetme/indirme aktiviteleri.
/// Veriler tarih bazlı kalıcıdır (UserAiUsages günlük satırlar + UserActivities olayları), bu yüzden
/// hem geçmişe dönük hem anlık (bugün) raporlanabilir.
/// </summary>
public interface IUsageReportService
{
    /// <summary>Tarih aralığında kullanıcı başına toplam kullanım + genel toplamlar.</summary>
    Task<UsageReportResponse> GetReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Tarih aralığında gün gün toplam kullanım (tüm kullanıcılar) — trend için.</summary>
    Task<IReadOnlyList<UsageDailyPoint>> GetDailyTotalsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

public record UsageReportRow(
    Guid UserId,
    string? Username,
    string Email,
    long TokensUsed,
    int AiRequests,
    int TemplateExports,
    int SaveCount,
    int DownloadCount);

public record UsageReportTotals(
    long TokensUsed,
    int AiRequests,
    int TemplateExports,
    int SaveCount,
    int DownloadCount,
    int UserCount);

public record UsageReportResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<UsageReportRow> Rows,
    UsageReportTotals Totals);

public record UsageDailyPoint(
    DateOnly Date,
    long TokensUsed,
    int AiRequests,
    int TemplateExports,
    int SaveCount,
    int DownloadCount);
