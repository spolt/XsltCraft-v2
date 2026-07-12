using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

/// <summary>
/// Örnek (exemplar) seçicinin saf (DB'siz) doğrulaması: token örtüşme eşiği,
/// top-2 sıralama, Türkçe folding ve boş/kısa istek davranışı.
/// </summary>
public class ExemplarScorerTests
{
    private static readonly DateTime T0 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ExemplarCandidate Cand(string q, int daysAfter = 0, bool global = false)
        => new(q, "cevap: " + q, global, T0.AddDays(daysAfter));

    [Fact]
    public void Returns_empty_when_request_blank()
    {
        var result = ExemplarScorer.Select("", [Cand("satıcı adresini gizle")]);
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_empty_when_request_has_too_few_tokens()
    {
        // Tek anlamlı token → MinOverlap (2) sağlanamaz.
        var result = ExemplarScorer.Select("adres", [Cand("satıcı adresini gizle")]);
        Assert.Empty(result);
    }

    [Fact]
    public void Returns_empty_when_overlap_below_threshold()
    {
        // "fatura tarihi" ile "satıcı adresi" arasında ortak anlamlı token yok.
        var result = ExemplarScorer.Select("fatura tarihini biçimlendir", [Cand("satıcı adresini gizle")]);
        Assert.Empty(result);
    }

    [Fact]
    public void Selects_candidate_with_sufficient_overlap()
    {
        var result = ExemplarScorer.Select(
            "satıcı adresini nasıl gizlerim",
            [Cand("satıcı adresini gizle")]);

        Assert.Single(result);
        Assert.Equal("satıcı adresini gizle", result[0].Question);
    }

    [Fact]
    public void Turkish_folding_matches_across_diacritics()
    {
        // "işe yaramadı" ↔ "ise yaramadi" aynı foldingden geçmeli.
        var result = ExemplarScorer.Select(
            "ISE YARAMADI neden",
            [Cand("işe yaramadı çözümü")]);

        Assert.Single(result);
    }

    [Fact]
    public void Takes_top_two_ordered_by_score_then_recency()
    {
        var candidates = new[]
        {
            Cand("satıcı adresini gizle", daysAfter: 1),                     // 2 örtüşme
            Cand("satıcı adresini şehir ilçe göster", daysAfter: 2),        // 3 örtüşme (en yüksek)
            Cand("satıcı adresini kaldır tamamen", daysAfter: 5),           // 2 örtüşme, daha yeni
            Cand("alıcı bilgisi", daysAfter: 0),                            // örtüşme yok
        };

        var result = ExemplarScorer.Select("satıcı adresini şehir ilçe olarak göster", candidates);

        Assert.Equal(2, result.Count);
        // En yüksek skorlu ilk sırada.
        Assert.Equal("satıcı adresini şehir ilçe göster", result[0].Question);
        // İkinci sıra: eşit skorlular arasında en yeni (daysAfter:5).
        Assert.Equal("satıcı adresini kaldır tamamen", result[1].Question);
    }

    [Fact]
    public void Returns_empty_when_no_candidates()
    {
        var result = ExemplarScorer.Select("satıcı adresini gizle", []);
        Assert.Empty(result);
    }
}
