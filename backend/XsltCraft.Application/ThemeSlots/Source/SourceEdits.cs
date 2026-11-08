using System.Text;

namespace XsltCraft.Application.ThemeSlots.Source;

/// <summary>Kaynak metne ofset tabanlı düzenlemeler. Ofsetler özgün metne göredir (ofset kayması olmaz);
/// çakışan aralıklar hata verir.</summary>
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
        // Önce büyük ofset; aynı başlangıçta değiştirme eklemeden sonra gelir (ekleme aralığın önüne yazılır); aynı noktaya
        // yapılan eklemeler eklenme sırasıyla yan yana kalır.
        var ordered = _edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.End).ThenByDescending(e => e.Seq).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var later = ordered[i - 1];
            var earlier = ordered[i];
            if (earlier.End > later.Start)
                throw new InvalidOperationException("Çakışan kaynak düzenlemesi.");
        }

        // Çakışma kontrolünden sonra aralıklar sıralı ve ayrık: artan sırada tek geçişte doğrusal kopya.
        var result = new StringBuilder(source.Length);
        var cursor = 0;
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var (start, end, text, _) = ordered[i];
            if (end > source.Length)
                throw new InvalidOperationException("Düzenleme kaynak sınırını aşıyor.");
            result.Append(source, cursor, start - cursor).Append(text);
            cursor = end;
        }
        return result.Append(source, cursor, source.Length - cursor).ToString();
    }
}
