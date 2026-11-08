using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.EasyEdit.Banks;

/// <summary>
/// Belgenin kendi metnine yazılmış banka/IBAN satırlarını bulur (XSLT çalıştırılmaz). Tutucu: kök şablon gövdesinde,
/// içeriği yalnız metin olan bir literal eleman ya da <c>xsl:text</c>; metninde tam bir IBAN geçer. Döngü, değer üreten
/// yapı, görsel bloğu, sabit not ve script/style/QR bölgeleri dışarıda kalır; <c>value-of</c> ile üretilen IBAN tespit
/// edilmez. Okunamayan durumda <see cref="DocumentBankDetection.None"/> döner.
/// </summary>
internal static partial class DocumentBankDetector
{
    public const int MaxRows = 20;
    public const int MaxTemplateRowChars = 4096;

    private const int MaxHolderChars = 2000;
    private const int MatchTimeoutMs = 2000;
    private const string XcAttributePrefix = "data-xc-";

    [GeneratedRegex(@"(?i)\bIBAN\s*(?:NO\.?)?\s*[:：]?\s*$", RegexOptions.CultureInvariant, MatchTimeoutMs)]
    private static partial Regex IbanLabel();

    // Ad aralığının kenarlarındaki dolgu: boşluk, bölünmez boşluk varlıkları ve ayraçlar.
    private const string Padding = @"(?:[\s|:\-–—]|&(?:#x0*a0|#0*160|nbsp);)+";

    [GeneratedRegex(@"\A" + Padding, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex LeadingPadding();

    [GeneratedRegex(Padding + @"\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.RightToLeft, MatchTimeoutMs)]
    private static partial Regex TrailingPadding();

    /// <summary>Ad hücresinde metin tutucuya inen tek çocuklu sarmalayıcılar.</summary>
    private static readonly HashSet<string> NameWrappers =
        ["span", "b", "strong", "font", "i", "em", "p", "div", "u", "small"];

    private const int MaxWrapperDepth = 8;

    private sealed record Holder(XElement Element, XElement Row, int IbanStart, int IbanEnd, string Iban);

    private readonly record struct NameRange(string Value, int? Start, int? End, bool Editable);

    public static DocumentBankDetection Detect(string xslt, DocumentType type)
    {
        try
        {
            return DetectCore(xslt, type);
        }
        catch (Exception ex) when (ex is RegexMatchTimeoutException or InvalidOperationException or ArgumentException
                                       or KeyNotFoundException or XmlException or IndexOutOfRangeException)
        {
            return DocumentBankDetection.None;
        }
    }

    private static DocumentBankDetection DetectCore(string xslt, DocumentType type)
    {
        if (string.IsNullOrEmpty(xslt) || !xslt.Contains("TR", StringComparison.Ordinal)
            || !XsltSourceMap.TryBuild(xslt, out var map, out _) || ThemeDocument.TryCreate(map, type) is not { } d)
            return DocumentBankDetection.None;

        var blocks = OverridesBlockText.Scan(xslt).Blocks;
        var resolver = new InsertionPointResolver(d);

        var holders = new List<Holder>();
        foreach (var h in d.Body.DescendantsAndSelf())
        {
            if (TryHolder(map, h, out var rawStart) is not { } m)
                continue;
            if (InBlock(blocks, map.Span(h).Start) || IsExcluded(h)
                || h.AncestorsAndSelf().Any(a => a.Attribute(FixedNoteWriter.MarkerAttribute) is not null)
                || !resolver.IsValidContext(IsXsl(h) ? h.Parent! : h))
                continue;

            var row = LiteralAncestorOrSelf(h, "tr") ?? (IsXsl(h) ? LiteralParent(h) : h);
            if (row is null)
                continue;
            holders.Add(new Holder(h, row, rawStart + m.Index, rawStart + m.Index + m.Length, m.Value));
        }

        if (holders.Count > 4 * MaxRows)
            return DocumentBankDetection.None;

        // Satır alt ağacında ikinci bir IBAN tutucusu varsa (iç içe satırlar dahil) satır atlanır.
        var holdersInSubtree = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var o in holders)
        {
            foreach (var a in o.Element.AncestorsAndSelf())
                holdersInSubtree[a] = holdersInSubtree.GetValueOrDefault(a) + 1;
        }
        var kept = holders.Where(x => holdersInSubtree.GetValueOrDefault(x.Row) == 1).ToList();
        if (kept.Count == 0)
            return DocumentBankDetection.None;

        var truncated = kept.Count > MaxRows;
        if (truncated)
            kept = kept.Take(MaxRows).ToList();

        var tableIds = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
        var rows = new List<DocumentBankRow>(kept.Count);
        for (var i = 0; i < kept.Count; i++)
        {
            var x = kept[i];
            var rowSpan = map.Span(x.Row);
            var name = IsLiteral(x.Row, "tr") ? CellName(map, x) : PrefixName(map, x);

            int? tableId = null;
            if (IsLiteral(x.Row, "tr") && LiteralAncestor(x.Row, "table") is { } table)
            {
                if (!tableIds.TryGetValue(table, out var id))
                    tableIds[table] = id = tableIds.Count;
                tableId = id;
            }

            var deletable = !ReferenceEquals(x.Row, d.Body) && IsPlain(x.Row, allowText: true)
                            && !blocks.Any(b => b.Start < rowSpan.End && rowSpan.Start < b.End);
            rows.Add(new DocumentBankRow(i, name.Value, x.Iban, name.Editable, deletable, map.LineOf(x.IbanStart),
                rowSpan.Start, rowSpan.End, x.IbanStart, x.IbanEnd, name.Start, name.End, tableId));
        }

        var tables = tableIds
            .OrderBy(t => t.Value)
            .Select(t => Table(map, t.Key, t.Value, rows, kept, truncated, blocks))
            .ToList();

        var (canAdd, insertOffset, indent) = AddPoint(xslt, rows, kept, truncated);
        return new DocumentBankDetection(true, canAdd, rows, tables, insertOffset, indent);
    }

    // ── Tutucu ────────────────────────────────────────────────────────────────

    /// <summary>Eleman bir tutucu adayıysa ham metnindeki tek IBAN eşleşmesi.</summary>
    private static Match? TryHolder(XsltSourceMap map, XElement h, out int rawStart)
    {
        rawStart = 0;
        if (IsXsl(h) ? !IsXsl(h, "text") || h.Attribute("disable-output-escaping") is not null : IsVoidElement(h))
            return null;
        if (!TryPureText(map, h, out var raw, out rawStart) || !raw.Contains("TR", StringComparison.Ordinal))
            return null;

        var matches = TrIban.Find().Matches(raw);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>İçeriği yalnız metin (CDATA hariç) ve en çok <see cref="MaxHolderChars"/> karakter mi?</summary>
    private static bool TryPureText(XsltSourceMap map, XElement e, out string raw, out int rawStart)
    {
        raw = "";
        rawStart = 0;
        var span = map.Span(e);
        if (span.SelfClosing || !e.Nodes().Any() || !e.Nodes().All(n => n is XText))
            return false;
        var length = span.EndTagStart - span.StartTagEnd;
        if (length > MaxHolderChars)
            return false;
        raw = map.Source.Substring(span.StartTagEnd, length);
        rawStart = span.StartTagEnd;
        return !raw.Contains('<');
    }

    private static bool InBlock(IReadOnlyList<BlockRange> blocks, int offset) =>
        blocks.Any(b => offset >= b.Start && offset < b.End);

    /// <summary>Alt ağaçta (<paramref name="allowText"/> ise <c>xsl:text</c> dışında) XSL, <c>img</c> ya da
    /// <c>data-xc-*</c> niteliği yok.</summary>
    private static bool IsPlain(XElement root, bool allowText) => root.DescendantsAndSelf().All(e =>
        (!IsXsl(e) || (allowText && IsXsl(e, "text")))
        && !IsLiteral(e, "img")
        && !e.Attributes().Any(a => a.Name.LocalName.StartsWith(XcAttributePrefix, StringComparison.Ordinal)));

    // ── Banka adı ─────────────────────────────────────────────────────────────

    /// <summary>Tablo satırı: IBAN hücresi dışındaki ilk saf metin hücresi.</summary>
    private static NameRange CellName(XsltSourceMap map, Holder x)
    {
        var ibanCell = x.Element.AncestorsAndSelf().FirstOrDefault(a => IsLiteral(a, "td") || IsLiteral(a, "th"));
        foreach (var cell in x.Row.Descendants().Where(c => IsLiteral(c, "td") || IsLiteral(c, "th")))
        {
            if (ReferenceEquals(cell, ibanCell) || !ReferenceEquals(LiteralAncestor(cell, "tr"), x.Row))
                continue;
            if (CellTextHolder(map, cell) is not { } textHolder || !TryPureText(map, textHolder, out var raw, out var rawStart))
                continue;
            if (TrIban.Find().IsMatch(raw))
                continue;

            var name = Range(raw, rawStart, 0, raw.Length);
            if (name.Value.Length == 0 || IbanLabel().Replace(name.Value, "").Trim().Length == 0)
                continue;
            return name;
        }
        return new NameRange("", null, null, false);
    }

    /// <summary>Hücrenin metin taşıyıcısı: yalnız metin içeren hücre ya da hücreden başlayan tek çocuklu sarmalayıcı
    /// zincirinin (<see cref="NameWrappers"/>, boşluk metinleri yok sayılır) sonundaki yalnız metin içeren eleman veya
    /// <c>xsl:text</c>.</summary>
    private static XElement? CellTextHolder(XsltSourceMap map, XElement cell)
    {
        var current = cell;
        for (var depth = 0; depth <= MaxWrapperDepth; depth++)
        {
            var nodes = current.Nodes().ToList();
            if (nodes.Count > 0 && nodes.All(n => n is XText))
                return current;

            var elements = nodes.OfType<XElement>().ToList();
            if (elements.Count != 1
                || !nodes.All(n => n is XElement || (n is XText t and not XCData && string.IsNullOrWhiteSpace(t.Value))))
                return null;

            var child = elements[0];
            if (IsXsl(child, "text"))
                return child.Attribute("disable-output-escaping") is null && !map.Span(child).SelfClosing ? child : null;
            if (IsXsl(child) || !NameWrappers.Contains(child.Name.LocalName) || map.Span(child).SelfClosing)
                return null;
            current = child;
        }
        return null;
    }

    /// <summary>Tablo dışı satır: IBAN'dan önceki önek, "IBAN:" etiketi ve ayraçlar kırpılarak.</summary>
    private static NameRange PrefixName(XsltSourceMap map, Holder x)
    {
        var rawStart = map.Span(x.Element).StartTagEnd;
        var prefix = map.Source[rawStart..x.IbanStart];

        var end = prefix.Length;
        if (TrailingPadding().Match(prefix) is { Success: true } padding)
            end = padding.Index;
        var label = IbanLabel().Match(prefix[..end]);
        if (label.Success)
            end = label.Index;

        return Range(prefix, rawStart, 0, end);
    }

    /// <summary>Ham parçanın [from, to) aralığı kenarlardaki boşluk, bölünmez boşluk varlıkları ve ayraçlardan kırpılır;
    /// değer çözülmüş metindir. Kırpılan parçada varlık (<c>&amp;</c>) yoksa ad düzenlenebilir.</summary>
    private static NameRange Range(string raw, int rawStart, int from, int to)
    {
        var segment = raw[from..to];
        if (LeadingPadding().Match(segment) is { Success: true } lead)
            from += lead.Length;
        segment = raw[from..to];
        if (segment.Length > 0 && TrailingPadding().Match(segment) is { Success: true } trail)
            to = from + trail.Index;
        if (from >= to)
            return new NameRange("", null, null, false);

        var piece = raw[from..to];
        var value = WebUtility.HtmlDecode(piece).Replace("\r\n", "\n").Trim();
        return new NameRange(value, rawStart + from, rawStart + to, !piece.Contains('&'));
    }

    // ── Tablo ve ekleme ───────────────────────────────────────────────────────

    private static DocumentBankTable Table(XsltSourceMap map, XElement table, int id, List<DocumentBankRow> rows,
        List<Holder> kept, bool truncated, IReadOnlyList<BlockRange> blocks)
    {
        var span = map.Span(table);
        var indexes = rows.Where(r => r.TableId == id).Select(r => r.Index).ToList();
        var detected = indexes.Select(i => kept[i].Row).ToHashSet(ReferenceEqualityComparer.Instance);

        var removable = !truncated
                        && IsPlain(table, allowText: false)
                        && !blocks.Any(b => b.Start < span.End && span.Start < b.End)
                        && table.Descendants().Where(e => IsLiteral(e, "tr")).All(tr => detected.Contains(tr) || IsHeaderRow(tr));
        return new DocumentBankTable(id, span.Start, span.End, removable, indexes);
    }

    /// <summary>Başlık satırı: yalnız hücreler; hücreler saf literal metin ve IBAN içermez.</summary>
    private static bool IsHeaderRow(XElement tr) =>
        tr.Nodes().All(n => n switch
        {
            XText t => t is not XCData && string.IsNullOrWhiteSpace(t.Value),
            XComment => true,
            XElement c => IsLiteral(c, "td") || IsLiteral(c, "th"),
            _ => false,
        })
        && tr.Elements().All(c => c.Nodes().All(n => n is XText and not XCData))
        && !TrIban.Find().IsMatch(tr.Value);

    private static (bool CanAdd, int? Offset, string? Indent) AddPoint(string xslt, List<DocumentBankRow> rows,
        List<Holder> kept, bool truncated)
    {
        if (truncated || rows.Count == 0 || rows.Count >= ThemeComposeRequestValidator.MaxBankRows)
            return (false, null, null);

        var parent = kept[0].Row.Parent;
        if (parent is null || kept.Any(k => !ReferenceEquals(k.Row.Parent, parent)))
            return (false, null, null);

        var template = rows[^1];
        var element = kept[^1].Row;
        if (!template.Deletable || !template.BankNameEditable || element.Attribute("id") is not null
            || template.RowEnd - template.RowStart > MaxTemplateRowChars || !IsCopyable(element))
            return (false, null, null);

        return (true, template.RowEnd, LeadingIndent(xslt, template.RowStart));
    }

    /// <summary>Şablon satır kopyalanabilir mi: <c>disable-output-escaping</c> taşıyan <c>xsl:text</c> yok ve
    /// literal nitelik değerlerinde <c>{</c> geçmiyor.</summary>
    private static bool IsCopyable(XElement row) => row.DescendantsAndSelf().All(e => IsXsl(e)
        ? !IsXsl(e, "text") || e.Attribute("disable-output-escaping") is not { } doe || doe.Value.Trim() == "no"
        : !e.Attributes().Any(a => a.Value.Contains('{')));

    /// <summary>Satırın önündeki boşluk; satır sonu varsa son satır sonundan itibaren.</summary>
    private static string LeadingIndent(string source, int start)
    {
        var i = start;
        while (i > 0 && char.IsWhiteSpace(source[i - 1]))
            i--;
        var ws = source[i..start];
        var nl = ws.LastIndexOf('\n');
        if (nl > 0 && ws[nl - 1] == '\r')
            nl--;
        return nl >= 0 ? ws[nl..] : ws;
    }
}
