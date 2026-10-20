namespace XsltCraft.Application.ThemeSlots.Source;

/// <summary>Kaynak metne ofset tabanlı düzenlemeler. Düzenlemeler azalan ofsetle uygulanır (ofset kayması
/// olmaz); çakışan aralıklar hata verir.</summary>
internal sealed class SourceEdits
{
    private readonly List<(int Start, int End, string Text, int Seq)> _edits = [];

    public int Count => _edits.Count;

    public void Insert(int offset, string text) => Replace(offset, offset, text);

    public void Replace(int start, int end, string text)
    {
        if (start < 0 || end < start)
            throw new ArgumentOutOfRangeException(nameof(start));
        _edits.Add((start, end, text, _edits.Count));
    }

    public string Apply(string source)
    {
        // Aynı noktaya yapılan eklemeler eklenme sırasıyla yan yana kalsın: önce büyük ofset, eşitlikte büyük sıra.
        var ordered = _edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Seq).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var later = ordered[i - 1];
            var earlier = ordered[i];
            if (earlier.End > later.Start)
                throw new InvalidOperationException("Çakışan kaynak düzenlemesi.");
        }

        var result = source;
        foreach (var (start, end, text, _) in ordered)
        {
            if (end > result.Length)
                throw new InvalidOperationException("Düzenleme kaynak sınırını aşıyor.");
            result = string.Concat(result.AsSpan(0, start), text, result.AsSpan(end));
        }
        return result;
    }
}
