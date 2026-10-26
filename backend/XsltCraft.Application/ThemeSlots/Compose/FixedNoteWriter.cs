using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <param name="Supported">Temada sabit not yazılabilecek bir Notlar alanı var mı (iki konum da geçerli).</param>
/// <param name="NotFound">Not istendi ama Notlar alanı yok → not uygulanmadı (uyarı).</param>
/// <param name="Error">Kapalı başarısızlık (stil beyaz listesi dışı karakter).</param>
internal sealed record FixedNoteWriteResult(string Xslt, bool Supported, bool Applied, bool NotFound, string? Error);

/// <summary>
/// Sabit notları temanın Notlar alanına yazar. <c>before</c>: koşulsuz <c>cbc:Note</c> döngüsünün hemen öncesi;
/// <c>after</c>: döngüyü içeren en yakın literal <c>td</c>/<c>th</c> (yoksa literal ebeveyn) elemanının içerik sonu,
/// geçersizse döngünün hemen sonrası. Not bir literal <c>div[data-xc-fixed-note]</c>'dir; metin yalnız text node
/// (XSLT'de text node AVT değildir), <c>style</c> yalnız <see cref="FixedNoteStyles"/> beyaz listesinden. Yorum yazılmaz.
/// Not listesi verilirse (boş olsa da) önceki sabit notlar önce silinir (idempotent).
/// </summary>
internal static class FixedNoteWriter
{
    public const string MarkerAttribute = "data-xc-fixed-note";

    public static FixedNoteWriteResult Apply(string xslt, IReadOnlyList<FixedNoteInput>? notes, DocumentType type)
    {
        var source = notes is null ? xslt : RemoveExisting(xslt);
        var points = FindPoints(source, type);
        var supported = points is not null;

        var wanted = (notes ?? []).Where(n => !string.IsNullOrWhiteSpace(n.Text)).ToList();
        if (wanted.Count == 0)
            return new(source, supported, false, false, null);
        if (points is not { } p)
            return new(source, false, false, true, null);

        var edits = new SourceEdits();
        foreach (var note in wanted.OrderBy(n => n.Position))
        {
            var css = FixedNoteStyles.ToCss(note);
            if (css.IndexOfAny(['{', '}', '"', '<', '&']) >= 0)
                return new(xslt, supported, false, false, "Not stili güvenli biçimde yazılamadı.");
            edits.Insert(note.Position == FixedNotePosition.Before ? p.Before : p.After, Fragment(note, css));
        }
        return new(edits.Apply(source), true, true, false, null);
    }

    private static string Fragment(FixedNoteInput note, string css)
    {
        var lines = note.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var div = new XElement("div",
            new XAttribute(MarkerAttribute, FixedNoteStyles.PositionValue(note.Position)),
            new XAttribute("style", css));
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                div.Add(new XElement("br"));
            if (lines[i].Length > 0)
                div.Add(new XText(lines[i]));
        }
        return div.ToString(SaveOptions.DisableFormatting);
    }

    private static (int Before, int After)? FindPoints(string source, DocumentType type)
    {
        if (!XsltSourceMap.TryBuild(source, out var map, out _) || ThemeDocument.TryCreate(map, type) is not { } d)
            return null;

        var loop = XsltNoteLoops.FindUnconditionalNoteLoop(map.Document);
        if (loop is null || !d.IsInBody(loop))
            return null;

        var resolver = new InsertionPointResolver(d);
        var before = resolver.Resolve(loop, PointKind.Before);
        var cell = loop.Ancestors().FirstOrDefault(a => IsLiteral(a, "td") || IsLiteral(a, "th")) ?? LiteralParent(loop);
        var after = (cell is null ? null : resolver.Resolve(cell, PointKind.ContentEnd)) ?? resolver.Resolve(loop, PointKind.After);

        return before is null || after is null ? null : (before.Offset, after.Offset);
    }

    private static string RemoveExisting(string source)
    {
        if (!source.Contains(MarkerAttribute, StringComparison.Ordinal) || !XsltSourceMap.TryBuild(source, out var map, out _))
            return source;

        var marked = map.Document.Descendants()
            .Where(e => IsLiteral(e) && e.Attribute(MarkerAttribute) is not null)
            .ToList();
        var edits = new SourceEdits();
        foreach (var e in marked.Where(e => !e.Ancestors().Any(marked.Contains)))
        {
            var span = map.Span(e);
            edits.Replace(span.Start, span.End, string.Empty);
        }
        return edits.Count == 0 ? source : edits.Apply(source);
    }
}
