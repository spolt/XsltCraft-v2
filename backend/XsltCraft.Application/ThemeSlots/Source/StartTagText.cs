namespace XsltCraft.Application.ThemeSlots.Source;

/// <param name="Start">Niteliğin önündeki boşluğun ofseti (etikete göre).</param>
/// <param name="ValueStart">Değerin ilk karakteri (açılış tırnağından sonra).</param>
/// <param name="ValueEnd">Kapanış tırnağının ofseti.</param>
internal readonly record struct TagAttribute(string Name, int Start, int ValueStart, int ValueEnd)
{
    public int End => ValueEnd + 1;
}

/// <param name="NameEnd">Eleman adının bittiği ofset.</param>
/// <param name="InsertAt">Yeni nitelik eklenecek ofset (son nitelikten hemen sonra).</param>
internal sealed record ParsedStartTag(string QualifiedName, int NameEnd, int InsertAt, IReadOnlyList<TagAttribute> Attributes)
{
    /// <summary>HTML nitelik adları büyük/küçük harf duyarsız eşlenir.</summary>
    public TagAttribute? Find(string name) =>
        Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) is { Name: not null } a ? a : null;
}

/// <summary>
/// İyi biçimli bir açılış etiketinin (<see cref="XsltSourceMap"/> aralığından) nitelikleri: doğrusal tarayıcı, düzenli
/// ifade yok. Değerler kaynak metindeki hâliyle (kaçışlı) okunur ve yazılır; etiketin geri kalanı korunur.
/// </summary>
internal static class StartTagText
{
    public static ParsedStartTag? Parse(string tag)
    {
        if (tag.Length < 2 || tag[0] != '<')
            return null;
        var i = 1;
        while (i < tag.Length && !IsSpace(tag[i]) && tag[i] is not ('/' or '>'))
            i++;
        var nameEnd = i;
        var attributes = new List<TagAttribute>();
        while (true)
        {
            var spaceStart = i;
            while (i < tag.Length && IsSpace(tag[i]))
                i++;
            if (i >= tag.Length)
                return null;
            if (tag[i] is '/' or '>')
                return new ParsedStartTag(tag[1..nameEnd], nameEnd, spaceStart, attributes);
            if (i == spaceStart)
                return null;

            var nameStart = i;
            while (i < tag.Length && !IsSpace(tag[i]) && tag[i] != '=')
                i++;
            var name = tag[nameStart..i];
            while (i < tag.Length && IsSpace(tag[i]))
                i++;
            if (i >= tag.Length || tag[i] != '=')
                return null;
            i++;
            while (i < tag.Length && IsSpace(tag[i]))
                i++;
            if (i >= tag.Length || tag[i] is not ('"' or '\''))
                return null;
            var valueEnd = tag.IndexOf(tag[i], i + 1);
            if (valueEnd < 0)
                return null;
            attributes.Add(new TagAttribute(name, spaceStart, i + 1, valueEnd));
            i = valueEnd + 1;
        }
    }

    /// <summary>Niteliğin kaynak metindeki (kaçışlı) değeri; yoksa ya da etiket okunamazsa null.</summary>
    public static string? Value(string tag, string name) =>
        Parse(tag)?.Find(name) is { } a ? tag[a.ValueStart..a.ValueEnd] : null;

    /// <summary>Niteliği yazar (varsa yerinde değiştirilir, yoksa <paramref name="after"/> niteliğinin ardına, o da
    /// yoksa son niteliğin ardına eklenir). <paramref name="rawValue"/> kaçışlanmış değerdir.</summary>
    public static string Set(string tag, string name, string rawValue, string? after = null)
    {
        var parsed = Parse(tag) ?? throw new InvalidOperationException("Açılış etiketi okunamadı.");
        var quote = rawValue.Contains('"') ? '\'' : '"';
        var text = $" {name}={quote}{rawValue}{quote}";
        if (parsed.Find(name) is { } existing)
            return string.Concat(tag.AsSpan(0, existing.Start), text, tag.AsSpan(existing.End));
        var at = after is not null && parsed.Find(after) is { } anchor ? anchor.End : parsed.InsertAt;
        return tag.Insert(at, text);
    }

    public static string Remove(string tag, string name)
    {
        var parsed = Parse(tag) ?? throw new InvalidOperationException("Açılış etiketi okunamadı.");
        return parsed.Find(name) is { } a ? string.Concat(tag.AsSpan(0, a.Start), tag.AsSpan(a.End)) : tag;
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\r' or '\n';
}
