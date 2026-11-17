using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XsltCraft.Application.Xslt.NoteLoops;

internal enum NotesLocateStatus { Found, None, Ambiguous }

/// <param name="InsertParent">Satırın gireceği eleman (tablo / tbody / hücre / koşul sarmalı).</param>
/// <param name="AfterElement">Varsa bu başlık elemanının hemen ardına.</param>
/// <param name="BeforeElement">Varsa bu elemanın hemen önüne (kapsayıcısız, gövdeye yazılmış not döngüsü).</param>
/// <param name="StyleLoop">Stilin türetileceği genel not döngüsü (yedek tabloda null).</param>
/// <param name="RowMode">Kapsayıcı satır (<c>tr</c>) bekliyor.</param>
internal sealed record NotesTarget(XElement InsertParent, XElement? AfterElement, XElement? BeforeElement, XElement? StyleLoop, bool RowMode);

internal sealed record NotesLocation(NotesLocateStatus Status, IReadOnlyList<NotesTarget> Targets, int CandidateGroups);

/// <summary>
/// Şablonun gerçek <b>Notlar</b> bölümünü tablo bazında bulur: genel (filtresiz / yalnız dışlayıcı filtreli) ve görünür
/// <c>cbc:Note</c> döngülerinin kapsayıcıları puanlanır (not adlandırması, KDV muafiyet/ödeme satırları +, belge
/// bilgisi −, kalem/toplam tabloları elenir). Aynı <c>id</c>'li (ya da aynı yapıdaki) kapsayıcılar kopya sayılır
/// (A5'te sayfa başına 2 kopya) ve her birine bir hedef üretilir.
/// </summary>
internal static class NotesSectionLocator
{
    private static readonly XNamespace Xsl = NoteLoopClassifier.Xsl;

    private static readonly Regex NoteNaming = new(@"(^|[\s_-])not|note|aciklama|açıklama|remark",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record Candidate(XElement Container, XElement? StyleLoop, bool RowMode, int Score, string GroupKey, int Order);

    public static NotesLocation Locate(XDocument doc, bool pickFirst)
    {
        var classifier = new NoteLoopClassifier(doc);
        var order = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var e in doc.Descendants())
            order[e] = order.Count;

        var candidates = new Dictionary<XElement, Candidate>(ReferenceEqualityComparer.Instance);
        foreach (var loop in doc.Descendants(Xsl + "for-each"))
        {
            if (!classifier.IsHeaderNoteLoop(loop, out var general) || !general || !classifier.IsVisible(loop))
                continue;

            var rowMode = EmitsRows(loop);
            var container = rowMode
                ? loop.Ancestors().FirstOrDefault(a => IsLiteral(a, "tbody") || IsLiteral(a, "thead")
                                                       || IsLiteral(a, "tfoot") || IsLiteral(a, "table"))
                : loop.Ancestors().FirstOrDefault(IsLiteral);
            if (container is null || candidates.ContainsKey(container) || HasLinesOrTotals(container))
                continue;

            candidates[container] = new Candidate(container, loop, rowMode, Score(container), GroupKey(container, loop), order[container]);
        }

        // Yedek: genel döngü yok ama not adlandırmalı, görünür ve cbc:Note okuyan en içteki tablo.
        if (candidates.Count == 0)
        {
            var tables = doc.Descendants()
                .Where(e => IsLiteral(e, "table") && HasNoteNaming(e) && ReferencesNote(e)
                            && !HasLinesOrTotals(e) && classifier.IsVisible(e))
                .ToList();
            // En içtekiler: başka bir aday tabloyu içeren tablolar elenir (her tablonun ataları bir kez yürünür).
            var set = new HashSet<XElement>(tables, ReferenceEqualityComparer.Instance);
            var outer = new HashSet<XElement>(ReferenceEqualityComparer.Instance);
            foreach (var t in tables)
                foreach (var a in t.Ancestors())
                    if (set.Contains(a))
                        outer.Add(a);
            foreach (var table in tables.Where(t => !outer.Contains(t)))
                candidates[table] = new Candidate(table, null, true, Score(table), GroupKey(table, null), order[table]);
        }

        if (candidates.Count == 0)
            return new NotesLocation(NotesLocateStatus.None, [], 0);

        var groups = candidates.Values
            .GroupBy(c => c.GroupKey)
            .Select(g => g.OrderBy(c => c.Order).ToList())
            .OrderBy(g => g[0].Order)
            .ToList();

        var best = groups.Max(g => g.Max(c => c.Score));
        var top = groups.Where(g => g.Max(c => c.Score) == best).ToList();
        if (top.Count > 1 && !pickFirst)
            return new NotesLocation(NotesLocateStatus.Ambiguous, [], top.Count);

        var targets = top[0].Select(ToTarget).ToList();
        return new NotesLocation(NotesLocateStatus.Found, targets, top.Count);
    }

    // ── Hedef / ekleme noktası ────────────────────────────────────────────

    private static NotesTarget ToTarget(Candidate c)
    {
        // Kapsayıcı sayfanın kendisiyse (body/html/form) "bölümün başı" = döngünün hemen önü.
        if (c.StyleLoop is { } loop && !c.RowMode && c.Container.Name.LocalName.ToLowerInvariant() is "body" or "html" or "form")
            return new NotesTarget(loop.Parent!, null, loop, loop, false);

        // Döngü ile kapsayıcı arasında koşul (ör. son sayfa) varsa not da o koşulun içinde kalmalı.
        if (c.StyleLoop is { } l
            && l.Ancestors().TakeWhile(a => !ReferenceEquals(a, c.Container))
                .LastOrDefault(a => a.Name.Namespace == Xsl && a.Name.LocalName is "if" or "when" or "otherwise") is { } cond)
            return new NotesTarget(cond, null, null, c.StyleLoop, c.RowMode);

        var parent = c.Container;
        if (c.RowMode && IsLiteral(parent, "table") && parent.Elements().FirstOrDefault(e => IsLiteral(e, "tbody")) is { } tbody)
            parent = tbody;

        XElement? after = null;
        var first = parent.Elements().FirstOrDefault();
        if (first is not null && IsLiteral(first)
            && (IsLiteral(first, "thead") || IsLiteral(first, "caption")
                || (IsLiteral(first, "tr") && first.Elements().Any(e => IsLiteral(e, "th")) && !first.Elements().Any(e => IsLiteral(e, "td")))))
            after = first;

        return new NotesTarget(parent, after, null, c.StyleLoop, c.RowMode || IsLiteral(parent, "table") || IsLiteral(parent, "tbody"));
    }

    // ── Puanlama ──────────────────────────────────────────────────────────

    private static int Score(XElement container)
    {
        var score = 10;
        var self = IsLiteral(container, "tbody") || IsLiteral(container, "thead") || IsLiteral(container, "tfoot")
            ? container.Parent ?? container
            : container;
        if (HasNoteNaming(container) || HasNoteNaming(self))
            score += 5;
        else if (self.Ancestors().Where(IsLiteral).Take(3).Any(HasNoteNaming))
            score += 2;

        var refs = XslExpressions(container).ToList();
        score += 2 * new[] { "TaxExemptionReason", "PaymentTerms", "PaymentMeans" }
            .Count(k => refs.Any(r => r.Contains(k, StringComparison.Ordinal)));
        score -= 5 * new[] { "cbc:UUID", "cbc:IssueDate" }
            .Count(k => refs.Any(r => r.Contains(k, StringComparison.Ordinal)));
        return score;
    }

    private static string GroupKey(XElement container, XElement? loop)
    {
        var self = IsLiteral(container, "tbody") ? container.Parent ?? container : container;
        if ((string?)self.Attribute("id") is { Length: > 0 } id)
            return "id:" + id;

        var template = container.Ancestors(Xsl + "template").FirstOrDefault();
        // Ata eleman-adı zinciri özet olarak tutulur (aday başına sabit boyut).
        var chain = new HashCode();
        foreach (var a in container.AncestorsAndSelf().TakeWhile(a => !ReferenceEquals(a, template)))
            chain.Add(a.Name);
        var tpl = (string?)template?.Attribute("name") ?? (string?)template?.Attribute("match") ?? "";
        var sel = NoteLoopClassifier.Collapse((string?)loop?.Attribute("select") ?? "");
        return $"sig:{tpl}|{chain.ToHashCode()}|{sel.GetHashCode()}";
    }

    private static bool HasNoteNaming(XElement e) =>
        IsLiteral(e) && (NoteNaming.IsMatch((string?)e.Attribute("id") ?? "") || NoteNaming.IsMatch((string?)e.Attribute("class") ?? ""));

    private static bool ReferencesNote(XElement e) =>
        XslExpressions(e).Any(r => Regex.IsMatch(r, @"(Invoice|DespatchAdvice)/([\w.-]+:)?Note\b"));

    private static bool HasLinesOrTotals(XElement e) =>
        e.Descendants().Any(d => d.Name.Namespace == Xsl
            && (string?)d.Attribute("select") is { } s
            && ((d.Name.LocalName is "for-each" or "apply-templates"
                 && (s.Contains("InvoiceLine", StringComparison.Ordinal) || s.Contains("DespatchLine", StringComparison.Ordinal)))
                || s.Contains("PayableAmount", StringComparison.Ordinal)));

    private static IEnumerable<string> XslExpressions(XElement e) =>
        e.DescendantsAndSelf()
            .Where(d => d.Name.Namespace == Xsl)
            .SelectMany(d => new[] { (string?)d.Attribute("select"), (string?)d.Attribute("test") })
            .OfType<string>()
            .Select(NoteLoopClassifier.Collapse);

    // Döngü gövdesi (XSL sarmalayıcıları açılarak) tablo satırı mı üretiyor?
    private static bool EmitsRows(XElement loop) => TopLiterals(loop).Any(e => IsLiteral(e, "tr"));

    private static IEnumerable<XElement> TopLiterals(XElement e)
    {
        foreach (var child in e.Elements())
        {
            if (IsLiteral(child))
                yield return child;
            else if (child.Name.LocalName is "if" or "choose" or "when" or "otherwise")
                foreach (var nested in TopLiterals(child))
                    yield return nested;
        }
    }

    private static bool IsLiteral(XElement e) => e.Name.Namespace != Xsl;

    private static bool IsLiteral(XElement e, string local) =>
        e.Name.Namespace != Xsl && e.Name.LocalName.Equals(local, StringComparison.OrdinalIgnoreCase);
}
