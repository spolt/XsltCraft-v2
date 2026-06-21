namespace XsltCraft.Application.Interfaces;

/// <summary>
/// Kullanıcı başına günlük kota (UTC tarih bazlı) yönetimi: AI istek/token ve şablon indirme.
/// Plan-bilinçlidir (<see cref="IEntitlementService"/> üzerinden); Editör/Admin'i bypass eder.
/// </summary>
public interface IUsageQuotaService
{
    /// <summary>AI isteği şu an izinli mi? (Free: günde 1 soru; Pro: 50k token; Editör/Admin: sınırsız.)</summary>
    Task<QuotaCheck> CheckAiAllowedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Bir AI isteğini kaydet: istek sayacı +1 ve yaklaşık token eklenir (tek upsert).</summary>
    Task IncrementAiAsync(Guid userId, int approximateTokens, CancellationToken ct = default);

    /// <summary>Production XSLT indirme şu an izinli mi? (Free: hayır; Pro: günde 3; Editör/Admin: sınırsız.)</summary>
    Task<QuotaCheck> CheckTemplateExportAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Bir başarılı indirme sonrası export sayacını +1 artır.</summary>
    Task IncrementTemplateExportAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kullanıcının bugünkü kullanımını döndür (frontend rozet/kalan kota için).</summary>
    Task<DailyUsage> GetUsageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Belirli bir gün için tüm kullanıcıların token kullanımı (admin raporu).</summary>
    Task<IReadOnlyList<UserAiUsageSummary>> GetDailyUsageAsync(DateOnly date, CancellationToken ct = default);
}

/// <summary>Kota kontrol sonucu. <see cref="Allowed"/> false ise <see cref="Reason"/> nedenini verir.</summary>
public record QuotaCheck(bool Allowed, QuotaDenyReason Reason = QuotaDenyReason.None);

public enum QuotaDenyReason
{
    None,
    /// <summary>Free kullanıcı günlük soru hakkını doldurdu → Pro'ya yönlendir.</summary>
    AiRequestLimit,
    /// <summary>Pro kullanıcı günlük token bütçesini doldurdu → yarın dene.</summary>
    AiTokenBudget,
    /// <summary>Plan production indirmeye izin vermiyor (Free) → Pro'ya yönlendir.</summary>
    ExportNotAllowed,
    /// <summary>Pro kullanıcı günlük 3 indirme hakkını doldurdu → yarın dene / ek paket.</summary>
    ExportDailyLimit,
}

public record DailyUsage(int AiRequestCount, int AiTokensUsed, int TemplateExportCount);

public record UserAiUsageSummary(Guid UserId, string? Username, string Email, int TokensUsed);
