using System.Text;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt.NoteLoops;

namespace XsltCraft.Application.Xslt;

/// <inheritdoc cref="IFixedNoteInjector"/>
public sealed class FixedNoteInjector : IFixedNoteInjector
{
    // Enjekte edilen blok bu yorumlar arasına alınır. XSLT yorumları çıktıya render olmaz;
    // tekrar uygulamada blokları bulup değiştirmemizi/eklememizi sağlar (idempotent).
    private const string MarkerStart = "<!-- xc:fixed-note:start -->";
    private const string MarkerEnd = "<!-- xc:fixed-note:end -->";
    private const string MarkerStartValue = " xc:fixed-note:start ";
    private const string MarkerEndValue = " xc:fixed-note:end ";

    // Kopya başına bir blok; pay elle kopyalanmış bloklar için. Tavan, söküm işini girdiden bağımsız küçük tutar.
    private const int MaxBlocks = 16;

    // Kopya (hedef) tavanı: A5 çift kopya 2; pay farklı yerleşimler için.
    private const int MaxTargets = 8;

    // Şablon kayıt tavanıyla aynı (UserXsltTemplateController.MaxXsltChars).
    private const int MaxXsltChars = 2 * 1024 * 1024;

    public FixedNoteResult Inject(string xslt, string noteText, FixedNoteMode mode,
        FixedNoteAmbiguity ambiguity = FixedNoteAmbiguity.Ask)
    {
        if (string.IsNullOrEmpty(xslt))
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "XSLT içeriği boş.");
        if (xslt.Length > MaxXsltChars)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "XSLT çok büyük.");
        if (!IsXmlText(noteText))
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Not metni geçersiz karakter içeriyor.");

        // Önceki tüm sabit not bloklarını çıkar — not her zaman güncel/doğru konuma yeniden yerleşir
        // (yanlış yere gömülmüş eski notlar kendini onarır).
        var (clean, inners) = StripMarkerBlocks(xslt);
        if (inners is null)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Şablonda çok fazla sabit not işareti var.");

        // Analiz ağaç kurar ve özyinelemeli klonlar: yapısal sınırlar (derinlik, eleman sayısı) önce, akış tabanlı.
        if (ThemeComposeXsltLimits.Check(clean) is { } limit)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, limit.Message);

        if (!XsltSourceMap.TryBuild(clean, out var map, out var parseError))
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, $"XSLT ayrıştırılamadı: {parseError}");

        var location = NotesSectionLocator.Locate(map.Document, ambiguity == FixedNoteAmbiguity.PickFirst);
        switch (location.Status)
        {
            case NotesLocateStatus.None:
                return new FixedNoteResult(FixedNoteStatus.NoNotesSection, xslt);
            case NotesLocateStatus.Ambiguous:
                return new FixedNoteResult(FixedNoteStatus.Ambiguous, xslt, Candidates: location.CandidateGroups);
        }

        var targets = location.Targets;
        if (targets.Count > MaxTargets)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Şablonda çok fazla not alanı kopyası var.");
        var inserts = new List<(int Offset, string Block)>(targets.Count);
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var snippet = FixedNoteSnippet.Build(target, noteText);

            // Append: önceki not(lar)ı koru; kopya sayısı eşleşiyorsa sırayla, değilse ilk bloğu her kopyada.
            var inner = mode == FixedNoteMode.Append && inners.Count > 0
                ? $"{(inners.Count == targets.Count ? inners[i] : inners[0])}\n{snippet}"
                : snippet;

            var offset = OffsetOf(map, target);
            if (offset < 0)
                return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Not bölümünün konumu belirlenemedi.");
            inserts.Add((offset, $"\n{MarkerStart}\n{inner}\n{MarkerEnd}"));
        }

        var sb = new StringBuilder(clean.Length + inserts.Sum(x => x.Block.Length));
        var cursor = 0;
        foreach (var (offset, block) in inserts.OrderBy(x => x.Offset))
        {
            sb.Append(clean, cursor, offset - cursor).Append(block);
            cursor = offset;
        }
        var updated = sb.Append(clean, cursor, clean.Length - cursor).ToString();
        if (updated.Length > MaxXsltChars)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Sabit not eklenince XSLT boyut sınırını aşıyor.");

        if (!Verify(updated, targets))
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Sabit not güvenli biçimde yerleştirilemedi.");

        var threat = XsltSafety.FindThreat(updated);
        if (threat is not null)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, threat);

        return new FixedNoteResult(FixedNoteStatus.Updated, updated, Targets: targets.Count);
    }

    private static bool IsXmlText(string text)
    {
        try
        {
            XmlConvert.VerifyXmlChars(text);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static int OffsetOf(XsltSourceMap map, NotesTarget target)
    {
        if (target.BeforeElement is { } before)
            return map.Span(before).Start;
        if (target.AfterElement is { } after)
            return map.Span(after).End;
        var span = map.Span(target.InsertParent);
        return span.SelfClosing ? -1 : span.StartTagEnd;
    }

    // Son koşul (fail-closed): sonuç iyi-biçimli, blok sayısı = kopya sayısı, her blok tek parça ve beklenen
    // ebeveyn adının altında.
    private static bool Verify(string updated, IReadOnlyList<NotesTarget> targets)
    {
        XDocument doc;
        try
        {
            using var xr = XmlReader.Create(new StringReader(updated), SecureXslt.ReaderSettings());
            doc = XDocument.Load(xr);
        }
        catch (XmlException)
        {
            return false;
        }

        var comments = doc.DescendantNodes().OfType<XComment>().ToList();
        var starts = comments.Where(c => c.Value == MarkerStartValue).ToList();
        var ends = comments.Where(c => c.Value == MarkerEndValue).ToList();
        if (starts.Count != targets.Count || ends.Count != targets.Count)
            return false;

        var expected = targets.Select(t => t.InsertParent.Name).OrderBy(n => n.ToString()).ToList();
        var actual = starts.Select(s => s.Parent?.Name).OrderBy(n => n?.ToString()).ToList();
        return starts.Zip(ends).All(p => ReferenceEquals(p.First.Parent, p.Second.Parent))
               && expected.SequenceEqual(actual);
    }

    // Tüm sabit not bloklarını (hemen öncesindeki tek satır sonu dahil) çıkarır; iç içerikleri belge sırasıyla döner.
    // Tavan aşılırsa Inners null.
    private static (string Clean, List<string>? Inners) StripMarkerBlocks(string xslt)
    {
        var inners = new List<string>();
        var clean = xslt;
        while (true)
        {
            var ms = clean.IndexOf(MarkerStart, StringComparison.Ordinal);
            if (ms < 0)
                break;
            var me = clean.IndexOf(MarkerEnd, ms, StringComparison.Ordinal);
            if (me < 0)
                break;
            if (inners.Count >= MaxBlocks)
                return (xslt, null);

            var inner = clean[(ms + MarkerStart.Length)..me].Trim();
            if (inner.Length > 0)
                inners.Add(inner);

            var removeStart = ms > 0 && clean[ms - 1] == '\n' ? ms - 1 : ms;
            clean = clean[..removeStart] + clean[(me + MarkerEnd.Length)..];
        }
        return (clean, inners);
    }
}
