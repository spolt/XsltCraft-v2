namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <summary>Blok aralığı: Start = START işaretinin başı, End = END işaretinin sonu; Inner işaretler arası.</summary>
internal readonly record struct BlockRange(int Start, int End, int InnerStart, int InnerEnd);

internal sealed record BlockScan(IReadOnlyList<BlockRange> Blocks, IReadOnlyList<(int Start, int Length)> Dangling)
{
    public bool WellFormed => Dangling.Count == 0 && Blocks.Count <= 1;
}

/// <summary>
/// Kullanıcı görsel bloğunun (<c>DEV-MODE-OVERRIDES</c>) işaret metinleri — tek kaynak. Blok, Tema Kullan /
/// Geliştirici Modu'nun indirilen/kaydedilen XSLT'ye eklediği betik + banka tablosudur.
/// </summary>
internal static class OverridesBlockText
{
    public const string Start = "<!-- DEV-MODE-OVERRIDES:START -->";
    public const string End = "<!-- DEV-MODE-OVERRIDES:END -->";

    /// <summary>Tüm START…END çiftlerini ve eşleşmeyen işaretleri bulur.</summary>
    public static BlockScan Scan(string xslt)
    {
        var markers = new List<(int Pos, bool IsStart)>();
        for (var i = xslt.IndexOf(Start, StringComparison.Ordinal); i >= 0; i = xslt.IndexOf(Start, i + 1, StringComparison.Ordinal))
            markers.Add((i, true));
        for (var i = xslt.IndexOf(End, StringComparison.Ordinal); i >= 0; i = xslt.IndexOf(End, i + 1, StringComparison.Ordinal))
            markers.Add((i, false));
        markers.Sort((a, b) => a.Pos.CompareTo(b.Pos));

        var blocks = new List<BlockRange>();
        var dangling = new List<(int, int)>();
        int? open = null;
        foreach (var (pos, isStart) in markers)
        {
            if (isStart)
            {
                if (open is not null)
                    dangling.Add((open.Value, Start.Length));
                open = pos;
            }
            else if (open is not null)
            {
                blocks.Add(new BlockRange(open.Value, pos + End.Length, open.Value + Start.Length, pos));
                open = null;
            }
            else
            {
                dangling.Add((pos, End.Length));
            }
        }
        if (open is not null)
            dangling.Add((open.Value, Start.Length));

        return new BlockScan(blocks, dangling);
    }

    /// <summary>Tüm blokları ve eşleşmeyen işaretleri siler (Tema Kullan'daki boş enjeksiyonla aynı kırpma).</summary>
    public static string RemoveAll(string xslt)
    {
        var scan = Scan(xslt);
        var ranges = scan.Blocks.Select(b => (b.Start, b.End - b.Start))
            .Concat(scan.Dangling)
            .OrderByDescending(r => r.Item1);

        var result = xslt;
        foreach (var (start, length) in ranges)
            result = result[..start].TrimEnd() + "\n" + result[(start + length)..].TrimStart();
        return result;
    }

    /// <summary>
    /// Bloğu ekler ya da mevcut tek bloğu değiştirir. Ekleme yeri bugünkü istemciyle aynı: son <c>&lt;/body&gt;</c>
    /// → son <c>&lt;/xsl:template&gt;</c> → dosya sonu. Birden çok blok ya da eşleşmeyen işaret varsa null
    /// (aradaki tasarımı silmemek için kapalı başarısızlık).
    /// </summary>
    public static string? Upsert(string xslt, string block, string xslPrefix)
    {
        var scan = Scan(xslt);
        if (!scan.WellFormed)
            return null;

        if (scan.Blocks.Count == 1)
        {
            var b = scan.Blocks[0];
            return xslt[..b.Start] + block + xslt[b.End..];
        }

        var bodyClose = xslt.LastIndexOf("</body>", StringComparison.Ordinal);
        if (bodyClose >= 0)
            return xslt[..bodyClose] + block + "\n" + xslt[bodyClose..];

        var tmpl = xslt.LastIndexOf($"</{xslPrefix}:template>", StringComparison.Ordinal);
        if (tmpl >= 0)
            return xslt[..tmpl] + block + "\n" + xslt[tmpl..];

        return xslt + "\n" + block;
    }

    public static string Wrap(string inner) => $"{Start}\n{inner}\n{End}";
}
