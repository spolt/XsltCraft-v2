using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.Validation;

namespace XsltCraft.Application.Xslt;

/// <inheritdoc cref="IFixedNoteInjector"/>
public sealed class FixedNoteInjector : IFixedNoteInjector
{
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    // Enjekte edilen blok bu yorumlar arasına alınır. XSLT yorumları çıktıya render olmaz;
    // tekrar uygulamada bloğu bulup değiştirmemizi/eklememizi sağlar (idempotent).
    private const string MarkerStart = "<!-- xc:fixed-note:start -->";
    private const string MarkerEnd = "<!-- xc:fixed-note:end -->";

    // Hedef: koşulsuz not gösterim döngüleri (e-Fatura + e-İrsaliye).
    private static readonly HashSet<string> NotePaths = new(StringComparer.Ordinal)
    {
        "//n1:Invoice/cbc:Note",
        "//n1:DespatchAdvice/cbc:Note",
    };

    public FixedNoteResult Inject(string xslt, string noteText, FixedNoteMode mode)
    {
        if (string.IsNullOrEmpty(xslt))
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "XSLT içeriği boş.");

        // Önceki sabit not bloğunu (varsa) çıkar — notu her zaman güncel/doğru konuma yeniden
        // yerleştireceğiz. Bu, daha önce yanlış yere (ör. bir xsl:variable içine) gömülmüş notların
        // tekrar uygulamada kendini onarmasını sağlar.
        var (cleanXslt, existingInner) = StripMarkerBlock(xslt);

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var sr = new StringReader(cleanXslt);
            using var xr = XmlReader.Create(sr, settings);
            doc = XDocument.Load(xr, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, $"XSLT ayrıştırılamadı: {ex.Message}");
        }

        var loop = FindUnconditionalNoteLoop(doc);

        // Görünür not bölümünde tutturacak koşulsuz döngü yok → kaynağı değiştirmeden bildir.
        if (loop is null)
            return new FixedNoteResult(FixedNoteStatus.NoNotesSection, xslt);

        var prefix = loop.GetPrefixOfNamespace(Xsl) ?? "xsl";
        var snippet = BuildSnippet(loop, noteText);

        // Append: önceki not(lar)ı koru ve doğru konuma birlikte taşı; Replace: yalnız yeni not.
        var inner = mode == FixedNoteMode.Append && !string.IsNullOrEmpty(existingInner)
            ? $"{existingInner}\n{snippet}"
            : snippet;

        var insertAt = FindLoopEndOffset(cleanXslt, loop, prefix);
        if (insertAt < 0)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, "Not döngüsünün konumu belirlenemedi.");

        var block = $"\n{MarkerStart}\n{inner}\n{MarkerEnd}";
        var updated = cleanXslt.Insert(insertAt, block);

        var threat = XsltSafety.FindThreat(updated);
        if (threat is not null)
            return new FixedNoteResult(FixedNoteStatus.Failed, xslt, threat);

        return new FixedNoteResult(FixedNoteStatus.Updated, updated);
    }

    // Görünür not bölümü, atasında ne koşul (if/when/choose) ne de değer-üretimi
    // (variable/param/with-param) bulunan bir for-each'tir. Bu yapıların içindeki not döngüleri
    // SGK değer çıkarımı gibi yardımcı hesaplardır (çıktıya doğrudan render edilmez) → atlanır.
    private static readonly string[] ExcludedAncestors =
        ["if", "when", "choose", "variable", "param", "with-param"];

    // --- Hedef döngüyü bul: select'i not yoluna eşit, dışlanan bir atası olmayan ilk for-each ---
    private static XElement? FindUnconditionalNoteLoop(XDocument doc) =>
        doc.Descendants(Xsl + "for-each")
            .Where(fe => fe.Attribute("select") is { Value: var sel } && NotePaths.Contains(Collapse(sel)))
            .FirstOrDefault(fe => !fe.Ancestors().Any(a =>
                a.Name.Namespace == Xsl && ExcludedAncestors.Contains(a.Name.LocalName)));

    // --- Snippet'i döngünün kendi gövdesinden türet (şablonun not stilini korur) ---
    private static string BuildSnippet(XElement? loop, string noteText)
    {
        var nodes = loop is not null ? DeriveTemplateNodes(loop, noteText) : DefaultNodes(noteText);
        return SerializeInner(nodes);
    }

    private static List<XNode> DeriveTemplateNodes(XElement loop, string noteText)
    {
        // Gövde tek bir koşul sarmalıysa (xsl:if veya xsl:choose/when) onu aç, içeriğini al.
        XElement source = loop;
        var elements = loop.Elements().ToList();
        if (elements.Count == 1 && elements[0].Name == Xsl + "if")
            source = elements[0];
        else if (elements.Count == 1 && elements[0].Name == Xsl + "choose"
                 && elements[0].Element(Xsl + "when") is { } when)
            source = when;

        var clones = source.Nodes().Select(CloneNode).ToList();

        // Not değerini yazan yer-tutucuyu (<xsl:value-of select="."/>) sabit metinle değiştir.
        var target = clones
            .OfType<XElement>()
            .SelectMany(e => e.DescendantsAndSelf(Xsl + "value-of"))
            .FirstOrDefault(v => Collapse(v.Attribute("select")?.Value ?? string.Empty) == ".");

        if (target is null)
            return DefaultNodes(noteText); // beklenmedik yapı → güvenli varsayılan

        var replacement = new XElement(Xsl + "text", noteText);
        if (target.Parent is null)
            clones[clones.IndexOf(target)] = replacement;
        else
            target.ReplaceWith(replacement);

        return clones;
    }

    private static List<XNode> DefaultNodes(string noteText) =>
    [
        new XElement("b", "      Not: "),
        new XElement(Xsl + "text", noteText),
        new XElement("br"),
    ];

    private static XNode CloneNode(XNode node) => node switch
    {
        XElement e => new XElement(e),
        XText t => new XText(t.Value),
        XComment c => new XComment(c.Value),
        _ => new XText(node.ToString()),
    };

    // Düğümleri xsl önekini koruyacak şekilde serialize edip dış sarmalayıcıyı sıyır.
    private static string SerializeInner(IEnumerable<XNode> nodes)
    {
        var wrap = new XElement(
            Xsl + "wrap",
            new XAttribute(XNamespace.Xmlns + "xsl", Xsl.NamespaceName),
            nodes);

        var s = wrap.ToString(SaveOptions.DisableFormatting);
        var open = s.IndexOf('>') + 1;
        var close = s.LastIndexOf("</", StringComparison.Ordinal);
        return s[open..close].Trim();
    }

    // Mevcut sabit not bloğunu (varsa, hemen öncesindeki tek satır sonu dahil) string'den çıkarır;
    // marker'lar arasındaki içeriği (önceki not satırları) ayrıca döndürür.
    private static (string Clean, string? Inner) StripMarkerBlock(string xslt)
    {
        var ms = xslt.IndexOf(MarkerStart, StringComparison.Ordinal);
        if (ms < 0)
            return (xslt, null);
        var me = xslt.IndexOf(MarkerEnd, StringComparison.Ordinal);
        if (me <= ms)
            return (xslt, null);

        var inner = xslt[(ms + MarkerStart.Length)..me].Trim();

        // Tekrarlı onarımda boş satır birikmemesi için bloğun önündeki tek satır sonunu da kaldır.
        var removeStart = ms > 0 && xslt[ms - 1] == '\n' ? ms - 1 : ms;
        var clean = xslt[..removeStart] + xslt[(me + MarkerEnd.Length)..];
        return (clean, inner.Length == 0 ? null : inner);
    }

    // for-each derinliğini sayarak hedef döngünün </xsl:for-each> bitiş ofsetini bul (dosyayı yeniden
    // serialize etmeden cerrahi ekleme için). Bulunamazsa -1.
    private static int FindLoopEndOffset(string xslt, XElement loop, string prefix)
    {
        if (loop is not IXmlLineInfo li || !li.HasLineInfo())
            return -1;

        var startName = OffsetOf(xslt, li.LineNumber, li.LinePosition);
        if (startName < 0)
            return -1;

        var openTag = $"<{prefix}:for-each";
        var closeTag = $"</{prefix}:for-each>";

        // '<' adın bir karakter solunda.
        var i = Math.Max(0, startName - 1);
        if (xslt[i] != '<')
            i = xslt.LastIndexOf('<', Math.Min(startName, xslt.Length - 1));
        if (i < 0)
            return -1;

        var depth = 0;
        while (i < xslt.Length)
        {
            if (MatchesAt(xslt, i, closeTag))
            {
                depth--;
                i += closeTag.Length;
                if (depth == 0)
                    return i;
            }
            else if (MatchesAt(xslt, i, openTag) && IsTagBoundary(xslt, i + openTag.Length))
            {
                var gt = xslt.IndexOf('>', i);
                if (gt < 0)
                    return -1;
                if (xslt[gt - 1] != '/') // kendiliğinden kapanmayan açılış → derinlik artar
                    depth++;
                i = gt + 1;
            }
            else
            {
                i++;
            }
        }

        return -1;
    }

    private static bool MatchesAt(string s, int i, string token) =>
        i + token.Length <= s.Length && string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

    // Açılış etiketi adının ardından gelen karakter ad-karakteri olmamalı (for-eachX'i eşleştirme).
    private static bool IsTagBoundary(string s, int i) =>
        i >= s.Length || char.IsWhiteSpace(s[i]) || s[i] is '>' or '/';

    private static int OffsetOf(string s, int line, int position)
    {
        if (line < 1 || position < 1)
            return -1;

        var currentLine = 1;
        var idx = 0;
        while (currentLine < line && idx < s.Length)
        {
            if (s[idx] == '\n')
                currentLine++;
            idx++;
        }

        var offset = idx + (position - 1);
        return offset <= s.Length ? offset : -1;
    }

    private static string Collapse(string value) => Regex.Replace(value, @"\s+", string.Empty);
}
