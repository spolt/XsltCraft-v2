using System.Text.RegularExpressions;

using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Analysis;

/// <param name="Xslt">Görselleri çıkarılmış metin.</param>
/// <param name="Decisions">Kalan görsellerin kararları (kaldırılan tasarım görselleri çıkmış, sırası korunmuş).</param>
/// <param name="OriginalIndex">Kalan her görselin özgün sıradaki yeri.</param>
/// <param name="Removed">Tamamen silinen görsellerin özgün sırası ve satırı.</param>
internal sealed record StripResult(string Xslt, IReadOnlyList<ThemeImageDecision>? Decisions,
    IReadOnlyList<int> OriginalIndex, IReadOnlyList<(int Index, int Line)> Removed);

/// <summary>
/// Admin'in yüklemede "temadan kaldır" dediği görseller, analizden önce kaynaktan çıkarılır:
/// tasarım görseli tamamen silinir; logo/imza görselinin yalnız kaynağı saydam 1×1 görselle değiştirilir — kutusu
/// (stil/boyut) ve yeri kalır, kullanıcının görseli oraya yerleşir, düzen kaymaz.
/// </summary>
internal static class ThemeImageStripper
{
    /// <summary>1×1 saydam GIF.</summary>
    public const string BlankImage = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

    private static readonly Regex SrcAttribute = new(@"\ssrc\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static StripResult Strip(string xslt, DocumentType type, IReadOnlyList<ThemeImageDecision>? decisions)
    {
        var identity = Enumerable.Range(0, decisions?.Count ?? 0).ToList();
        if (decisions is null || !decisions.Any(d => d.Strip))
            return new StripResult(xslt, decisions, identity, []);

        var source = ThemeSlotMarkers.Clean(xslt);
        if (!XsltSourceMap.TryBuild(source, out var map, out _) || ThemeDocument.TryCreate(map, type) is not { } doc)
            return new StripResult(xslt, decisions, identity, []);

        var detected = ThemeImageDetector.Detect(doc, new ThemeAnchorDetector().Detect(doc), new InsertionPointResolver(doc));
        if (detected.Count != decisions.Count)
            return new StripResult(xslt, decisions, identity, []); // analiz uyuşmazlığı uyarıyla raporlar

        var edits = new SourceEdits();
        var kept = new List<ThemeImageDecision>();
        var originalIndex = new List<int>();
        var removed = new List<(int, int)>();
        for (var i = 0; i < detected.Count; i++)
        {
            var d = decisions[i];
            var span = map.Span(detected[i].Element);
            if (d.Strip && d.Role == ThemeImageRole.Design)
            {
                edits.Replace(span.Start, span.End, string.Empty);
                removed.Add((i, detected[i].Line));
                continue;
            }
            if (d.Strip)
            {
                var tag = source[span.Start..span.StartTagEnd];
                var value = SrcAttribute.Match(tag).Groups["v"];
                if (value.Success)
                    edits.Replace(span.Start + value.Index, span.Start + value.Index + value.Length, BlankImage);
            }
            kept.Add(d with { Removable = d.Removable && !d.Strip });
            originalIndex.Add(i);
        }
        return new StripResult(edits.Apply(source), kept, originalIndex, removed);
    }
}
