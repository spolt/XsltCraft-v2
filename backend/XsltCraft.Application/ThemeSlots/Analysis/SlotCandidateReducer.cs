using System.Text.RegularExpressions;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <summary>Bir yuva + varsayılan atama sonucu.</summary>
internal sealed record ReducedSlot(SlotCandidate Candidate, string Id, ThemeAssetKind? DefaultFor);

/// <summary>
/// Aynı yere düşen adayları birleştirir (küçük sıra kazanır), id'leri tekilleştirir ve logo/imza için
/// varsayılan yuvaları seçer. Varsayılan önceliği: admin'in o rolle işaretlediği temadaki görsel; sonra eski tutucuyu (<c>#company_logo</c>, <c>#imza</c>…) benimseyen
/// yuva — bugünkü görünüm korunur; yoksa kural öncelikleri (logo R5 &gt; R1 &gt; R9 &gt; R3, imza R4 &gt; R6 &gt; R7 &gt; R9).
/// </summary>
internal static class SlotCandidateReducer
{
    private static readonly Regex Comments = new("<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static List<ReducedSlot> Reduce(ThemeDocument d, List<SlotCandidate> candidates, List<ThemeSlotWarning> warnings)
    {
        var kept = new List<SlotCandidate>();
        foreach (var c in candidates.OrderBy(c => c.Order))
        {
            var clash = kept.FirstOrDefault(k => SamePlace(d, k, c));
            if (clash is null)
            {
                kept.Add(c);
                continue;
            }
            warnings.Add(new ThemeSlotWarning("slot-merged",
                $"{c.Rule} ({c.Id}) konumu {clash.Rule} ({clash.Id}) ile aynı yere düştüğü için birleştirildi.", c.Id));
        }

        // Kimlikleri tekilleştir (R9 aynı türden iki tutucu üretebilir).
        var used = new HashSet<string>(StringComparer.Ordinal);
        var named = new List<(SlotCandidate C, string Id)>();
        foreach (var c in kept)
        {
            var id = c.Id;
            for (var n = 2; !used.Add(id); n++)
                id = $"{c.Id}-{n}";
            named.Add((c, id));
        }

        var logo = PickDefault(named, ThemeAssetKind.Logo, exclude: null);
        var signature = PickDefault(named, ThemeAssetKind.Signature, exclude: logo);

        return named.Select(n => new ReducedSlot(n.C, n.Id,
            n.Id == logo ? ThemeAssetKind.Logo : n.Id == signature ? ThemeAssetKind.Signature : null)).ToList();
    }

    private static string? PickDefault(List<(SlotCandidate C, string Id)> slots, ThemeAssetKind kind, string? exclude)
    {
        var available = slots.Where(s => s.Id != exclude).ToList();

        var existing = available.FirstOrDefault(s => s.C.PlaceholderKind == kind);
        if (existing.Id is not null)
            return existing.Id;

        foreach (var legacyId in LegacyHolderIds.For(kind))
        {
            var adopting = available.FirstOrDefault(s => (string?)s.C.AdoptedHolder?.Attribute("id") == legacyId);
            if (adopting.Id is not null)
                return adopting.Id;
        }

        return available
            .Where(s => s.C.DefaultKind == kind && s.C.DefaultPriority > 0)
            .OrderBy(s => s.C.Conditional)
            .ThenBy(s => s.C.DefaultPriority)
            .ThenBy(s => s.C.Order)
            .Select(s => s.Id)
            .FirstOrDefault();
    }

    private static bool SamePlace(ThemeDocument d, SlotCandidate a, SlotCandidate b)
    {
        if (a.AdoptedHolder is not null && ReferenceEquals(a.AdoptedHolder, b.AdoptedHolder))
            return true;
        if (a.Point is null || b.Point is null)
            return false;
        if (a.Point.Offset == b.Point.Offset)
            return true;
        if (!ReferenceEquals(a.Point.Parent, b.Point.Parent))
            return false;

        // Aynı ebeveynde, aralarında yalnız boşluk/yorum varsa görsel olarak aynı yerdir.
        var lo = Math.Min(a.Point.Offset, b.Point.Offset);
        var hi = Math.Max(a.Point.Offset, b.Point.Offset);
        var between = Comments.Replace(d.Map.Source[lo..hi], string.Empty);
        return string.IsNullOrWhiteSpace(between);
    }

    /// <summary>Benimsenen tutucunun id'si (rapor için).</summary>
    public static string? LegacyIdOf(SlotCandidate c) => (string?)c.AdoptedHolder?.Attribute("id");
}
