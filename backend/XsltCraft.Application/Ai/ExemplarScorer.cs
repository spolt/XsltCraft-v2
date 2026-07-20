namespace XsltCraft.Application.Ai;

/// <summary>Erişim için aday bir örnek (skorlama girdisi).</summary>
public record ExemplarCandidate(string Question, string Answer, bool IsGlobal, DateTime CreatedAt);

/// <summary>
/// Saf (DB'siz) örnek seçici. Yeni kullanıcı sorusu ile geçmiş başarılı soruların token
/// örtüşmesine göre en alakalı 1-2 örneği seçer. <see cref="PatternSelector"/>/<see cref="TextFold"/>
/// ile aynı normalizasyonu kullanır; embedding YOKTUR (v1).
/// </summary>
public static class ExemplarScorer
{
    public const int MaxExemplars = 2;
    public const int MinOverlap = 2;   // en az 2 ortak token yoksa hiç enjekte etme (alakasız örnek gürültüsü)
    private const int MinTokenLength = 3;

    public static IReadOnlyList<ExemplarCandidate> Select(
        string? userRequest,
        IReadOnlyList<ExemplarCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(userRequest) || candidates.Count == 0)
            return [];

        var requestTokens = Tokenize(userRequest);
        if (requestTokens.Count < MinOverlap)
            return [];

        var scored = new List<(ExemplarCandidate Candidate, int Score)>();
        foreach (var c in candidates)
        {
            var questionTokens = Tokenize(c.Question);
            var overlap = requestTokens.Count(questionTokens.Contains);
            if (overlap >= MinOverlap)
                scored.Add((c, overlap));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Candidate.CreatedAt)
            .Take(MaxExemplars)
            .Select(s => s.Candidate)
            .ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var folded = TextFold.Fold(text);
        var tokens = new HashSet<string>();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in folded)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                if (sb.Length >= MinTokenLength) tokens.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length >= MinTokenLength) tokens.Add(sb.ToString());
        return tokens;
    }
}
