using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Ai;

public class AiExemplarService(AppDbContext db, ILogger<AiExemplarService> logger) : IAiExemplarService
{
    // Bellekte skorlanacak aday havuzu (yakın geçmişten). Tek indeksli sorgu, ucuz.
    private const int CandidatePoolSize = 50;
    private const int AnswerClipChars = 2_000;

    public async Task<IReadOnlyList<AiExemplar>> GetExemplarsAsync(Guid userId, string? userRequest, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
            return [];

        try
        {
            var rows = await db.AiFeedbacks
                .AsNoTracking()
                .Where(f => f.Rating == AiFeedbackRating.Positive && (f.UserId == userId || f.IsGlobal))
                .OrderByDescending(f => f.CreatedAt)
                .Take(CandidatePoolSize)
                .Select(f => new { f.UserMessage, f.AssistantAnswer, f.IsGlobal, f.CreatedAt })
                .ToListAsync(ct);

            var candidates = rows
                .Select(r => new ExemplarCandidate(r.UserMessage, r.AssistantAnswer, r.IsGlobal, r.CreatedAt))
                .ToList();

            return ExemplarScorer.Select(userRequest, candidates)
                .Select(c => new AiExemplar(c.Question, Clip(c.Answer, AnswerClipChars)))
                .ToList();
        }
        catch (Exception ex)
        {
            // Öğrenme erişimi sohbeti asla kırmamalı — sessizce boş dön.
            logger.LogWarning(ex, "Exemplar erişimi başarısız; örneksiz devam ediliyor.");
            return [];
        }
    }

    private static string Clip(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
