using System.Text;

namespace XsltCraft.Application.Ai;

public static class PatternSelector
{
    private const int MaxPatterns = 4;

    // XSLT içerik sinyalleri: pattern id → XSLT token
    private static readonly (string PatternId, string XsltToken)[] XsltSignals =
    [
        ("invoice-note",          "cbc:Note"),
        ("invoice-line",          "cac:InvoiceLine"),
        ("legal-monetary-total",  "LegalMonetaryTotal"),
    ];

    public static IReadOnlyList<PromptPattern> Select(AiRequest req)
    {
        var candidateText = Fold((req.UserRequest ?? "") + " " + (req.Selection ?? ""));
        var xsltContext   = (req.Selection ?? "") + (req.UserXslt ?? "");

        var scored = new List<(string Id, int Score)>();

        foreach (var (id, pattern) in PromptRegistry.Patterns)
        {
            var score = 0;

            foreach (var trigger in pattern.Triggers)
            {
                if (candidateText.Contains(Fold(trigger), StringComparison.Ordinal))
                    score++;
            }

            foreach (var (signalId, token) in XsltSignals)
            {
                if (signalId == id && xsltContext.Contains(token, StringComparison.Ordinal))
                    score++;
            }

            if (score > 0)
                scored.Add((id, score));
        }

        if (scored.Count == 0)
        {
            var wordCount = candidateText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount >= 2 ? Fallback() : [];
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Id)
            .Take(MaxPatterns)
            .Select(s => PromptRegistry.Patterns[s.Id])
            .ToList();
    }

    private static IReadOnlyList<PromptPattern> Fallback()
    {
        var fallbackIds = new HashSet<string> { "invoice-header", "invoice-line" };
        return PromptRegistry.Patterns.Values
            .Where(p => fallbackIds.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToList();
    }

    // Türkçe karakter folding: ı/ş/ç/ğ/ü/ö → ASCII karşılıkları
    // ToLowerInvariant sonrası uygulanır; hem trigger hem aday metin aynı Fold'dan geçer.
    private static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            sb.Append(c switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                'ü' => 'u',
                'ö' => 'o',
                _ => c,
            });
        }
        return sb.ToString();
    }
}
