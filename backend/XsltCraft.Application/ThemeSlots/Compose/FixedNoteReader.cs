using System.Globalization;
using System.Text;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// <see cref="FixedNoteWriter"/>'ın yazdığı sabit notları (<c>div[data-xc-fixed-note]</c>) geri okur. Konum başına ilk
/// not alınır. Stil <see cref="FixedNoteStyles.ToCss"/>'in tersiyle çözülür; beyaz liste dışı değer güvenli
/// varsayılana düşer (istemcinin varsayılanlarıyla aynı).
/// </summary>
internal static class FixedNoteReader
{
    private const int DefaultFontSize = 11;
    private const string DefaultColor = "#000000";
    private const string DefaultFontFamily = "theme";

    public static IReadOnlyList<FixedNoteInput> Read(string xslt, DocumentType type)
    {
        if (!xslt.Contains(FixedNoteWriter.MarkerAttribute, StringComparison.Ordinal)
            || !XsltSourceMap.TryBuild(xslt, out var map, out _) || ThemeDocument.TryCreate(map, type) is not { } d)
            return [];

        var result = new List<FixedNoteInput>();
        foreach (var div in d.Body.Descendants().Where(e => ThemeDocument.IsLiteral(e, "div")))
        {
            if (!FixedNoteStyles.TryParsePosition((string?)div.Attribute(FixedNoteWriter.MarkerAttribute), out var position)
                || result.Any(n => n.Position == position))
                continue;

            result.Add(ToNote(position, TextOf(div), (string?)div.Attribute("style") ?? ""));
            if (result.Count == FixedNoteStyles.MaxNotes)
                break;
        }
        return result;
    }

    private static FixedNoteInput ToNote(FixedNotePosition position, string text, string style)
    {
        var css = ParseCss(style);
        string? Get(string key) => css.TryGetValue(key, out var v) ? v : null;

        var fontSize = Get("font-size") is { } fs && fs.EndsWith("px", StringComparison.Ordinal)
            && int.TryParse(fs[..^2], NumberStyles.None, CultureInfo.InvariantCulture, out var px) && FixedNoteStyles.FontSizes.Contains(px)
            ? px
            : DefaultFontSize;
        var color = Get("color") is { } c && FixedNoteStyles.ColorPattern.IsMatch(c) ? c.ToUpperInvariant() : DefaultColor;
        var family = Get("font-family") is { } f
            ? FixedNoteStyles.FontFamilies.FirstOrDefault(kv => kv.Value == f).Key ?? DefaultFontFamily
            : DefaultFontFamily;
        var align = Get("text-align") switch
        {
            "center" => FixedNoteAlign.Center,
            "right" => FixedNoteAlign.Right,
            _ => FixedNoteAlign.Left,
        };

        return new FixedNoteInput(position, text, Get("font-weight") == "bold", Get("font-style") == "italic",
            Get("text-decoration") == "underline", fontSize, color, family, align);
    }

    // "ad:değer;…" → sözlük (ilk değer geçerli). Değerler ToCss'teki gibi boşluksuz yazılır; kenar boşlukları kırpılır.
    private static Dictionary<string, string> ParseCss(string style)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in style.Split(';'))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0)
                continue;
            result.TryAdd(part[..colon].Trim(), part[(colon + 1)..].Trim());
        }
        return result;
    }

    // Düz metin düğümleri birleştirilir, <br> → satır sonu; istemci sınırlarıyla (satır/karakter) kırpılır.
    private static string TextOf(XElement div)
    {
        var sb = new StringBuilder();
        foreach (var node in div.Nodes())
        {
            if (node is XText t)
                sb.Append(t.Value);
            else if (node is XElement e && ThemeDocument.IsLiteral(e, "br"))
                sb.Append('\n');
        }

        var lines = sb.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var text = string.Join('\n', lines.Take(FixedNoteStyles.MaxLines));
        return text.Length > FixedNoteStyles.MaxTextLength ? text[..FixedNoteStyles.MaxTextLength] : text;
    }
}
