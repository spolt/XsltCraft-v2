using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces;

public record AiFeedbackInput(
    AiFeedbackRating Rating,
    string UserMessage,
    string AssistantAnswer,
    bool Applied);

public interface IAiFeedbackService
{
    /// <summary>Yeni geri bildirim kaydeder; metinleri sınırlarına kırpar. Kaydın Id'sini döner.</summary>
    Task<Guid> RecordAsync(Guid userId, AiFeedbackInput input, CancellationToken ct);

    /// <summary>
    /// Mevcut bir geri bildirimin oy/uygulama durumunu günceller (sahiplik doğrulanır → IDOR koruması).
    /// Kayıt yoksa veya kullanıcıya ait değilse false döner.
    /// </summary>
    Task<bool> UpdateAsync(Guid userId, Guid feedbackId, AiFeedbackRating rating, bool applied, CancellationToken ct);
}
