using Microsoft.EntityFrameworkCore;

using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Ai;

public class AiFeedbackService(AppDbContext db) : IAiFeedbackService
{
    // Satırları sınırlı tutmak için (DB kolon sınırlarıyla uyumlu).
    private const int UserMessageMax = 2_000;
    private const int AssistantAnswerMax = 8_000;

    public async Task<Guid> RecordAsync(Guid userId, AiFeedbackInput input, CancellationToken ct)
    {
        var feedback = new AiFeedback
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Rating = input.Rating,
            UserMessage = Clip(input.UserMessage, UserMessageMax),
            AssistantAnswer = Clip(input.AssistantAnswer, AssistantAnswerMax),
            Applied = input.Applied,
        };

        db.AiFeedbacks.Add(feedback);
        await db.SaveChangesAsync(ct);
        return feedback.Id;
    }

    public async Task<bool> UpdateAsync(Guid userId, Guid feedbackId, AiFeedbackRating rating, bool applied, CancellationToken ct)
    {
        // Sahiplik doğrulanır: yalnızca kendi kaydını güncelleyebilir.
        var updated = await db.AiFeedbacks
            .Where(f => f.Id == feedbackId && f.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Rating, rating)
                .SetProperty(f => f.Applied, applied), ct);
        return updated > 0;
    }

    private static string Clip(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max];
    }
}
