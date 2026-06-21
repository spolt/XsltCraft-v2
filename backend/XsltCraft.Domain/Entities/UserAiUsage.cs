namespace XsltCraft.Domain.Entities;

/// <summary>
/// Kullanıcı başına günlük kullanım sayaçları (UTC tarih bazlı, gece yarısı otomatik sıfırlanır).
/// AI token bütçesinin yanında AI soru sayısı (Standart 1/gün) ve şablon indirme (Pro 3/gün) da burada tutulur.
/// </summary>
public class UserAiUsage
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int TokensUsed { get; set; }
    /// <summary>Bugün yapılan AI isteği sayısı — Standart kullanıcının "günlük 1 soru" limiti için.</summary>
    public int AiRequestCount { get; set; }
    /// <summary>Bugün yapılan production XSLT indirme sayısı — Pro kullanıcının "günlük 3 şablon" limiti için.</summary>
    public int TemplateExportCount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
