using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Source;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>
/// Statik görsel düzenlemelerinin işaretleme parçaları — <c>ThemeAssetRuntime.js</c>'teki <c>replace()</c> ve
/// <c>build()</c>'in statik karşılıkları. Tüm değerler beyaz listeden (sayı, hizalama, düzen adı) gelir.
/// </summary>
internal static class StaticImageMarkup
{
    /// <summary>Statik yerleşimin sarmalayıcısı; görsel taşınır ya da silinirse sarmalayıcıyla birlikte kalkar.
    /// Yuva işareti değildir; temizleme ve analiz onu sıradan bir nitelik olarak görür.</summary>
    public const string WrapAttribute = "data-xc-static-wrap";

    /// <summary>Görselin kendi etiketine boyut/hizalama yazar; değer verilmediyse etiket değişmez.</summary>
    public static string StyleImage(string tag, int? width, int? height, ThemeAlignment? alignment)
    {
        var declarations = new List<(string, string)>();
        if (alignment is { } a)
        {
            declarations.Add(("object-fit", "contain"));
            declarations.Add(("object-position", Position(a)));
        }
        AddSize(declarations, width, height);
        return declarations.Count == 0 ? tag : WithStyle(tag, declarations);
    }

    /// <summary><c>replace()</c>: hedef görselin kutusu korunur, kaynağı ve rol işareti taşınan görselden alınır.</summary>
    public static string ReplaceInto(string targetTag, string sourceValue, string? kind, int? width, int? height, ThemeAlignment? alignment)
    {
        var tag = StartTagText.Set(targetTag, "src", sourceValue);
        tag = StartTagText.Remove(tag, "srcset");
        if (kind is not null)
        {
            // Kaldırma izni hedefteki türe aitti; tür değişince taşınmaz.
            if (StartTagText.Value(tag, ThemeSlotMarkers.PlaceholderAttribute) != kind)
                tag = StartTagText.Remove(tag, ThemeSlotMarkers.RemovableAttribute);
            tag = StartTagText.Set(tag, ThemeSlotMarkers.PlaceholderAttribute, kind, after: ThemeSlotMarkers.SlotAttribute);
        }
        var declarations = new List<(string, string)> { ("object-fit", "contain"), ("object-position", Position(alignment ?? ThemeAlignment.Left)) };
        AddSize(declarations, width, height);
        return WithStyle(tag, declarations);
    }

    /// <summary><c>build()</c>: görseli yuvanın düzenine göre hizalı bir kutuya sarar.</summary>
    /// <param name="slot">Sarmalayıcının yerini aldığı eklenen işaretin yuvası; temizleme bu niteliği siler.</param>
    public static string Wrapper(string prefix, string? layout, ThemeAlignment alignment, string inner, string? slot = null)
    {
        var justify = alignment switch
        {
            ThemeAlignment.Center => "center",
            ThemeAlignment.Right => "flex-end",
            _ => "flex-start",
        };
        var css = $"display:flex;align-items:center;justify-content:{justify};" + layout switch
        {
            "block-clear" => "clear:both;",
            "float-left" => "float:left;margin:0 8px 4px 0;",
            _ => string.Empty,
        };
        var slotAttribute = slot is null ? string.Empty : $@" {ThemeSlotMarkers.SlotAttribute}=""{slot}""";
        return $@"<{prefix}div{slotAttribute} {WrapAttribute}=""1"" style=""{css}"">{inner}</{prefix}div>";
    }

    /// <summary>Nitelikli addan önek (<c>h:img</c> → <c>h:</c>).</summary>
    public static string PrefixOf(string qualifiedName) =>
        qualifiedName.IndexOf(':') is var colon and > 0 ? qualifiedName[..(colon + 1)] : string.Empty;

    private static string Position(ThemeAlignment a) => a switch
    {
        ThemeAlignment.Center => "center center",
        ThemeAlignment.Right => "right center",
        _ => "left center",
    };

    private static void AddSize(List<(string, string)> declarations, int? width, int? height)
    {
        if (width is { } w)
            declarations.Add(("width", $"{w}px"));
        if (height is { } h)
            declarations.Add(("height", $"{h}px"));
        if (width is not null && height is null)
            declarations.Add(("height", "auto"));
        if (height is not null && width is null)
            declarations.Add(("width", "auto"));
    }

    private static string WithStyle(string tag, IReadOnlyList<(string Property, string Value)> declarations) =>
        StartTagText.Set(tag, "style", MergeStyle(StartTagText.Value(tag, "style"), declarations));

    // Mevcut stil korunur, aynı özellikler güncellenir. Değer şablonu ({…}) ya da varlık içeren stil ayrıştırılmaz:
    // yeni bildirimler sona eklenir (CSS'te sonraki kazanır).
    private static string MergeStyle(string? raw, IReadOnlyList<(string Property, string Value)> declarations)
    {
        var added = string.Join(';', declarations.Select(d => $"{d.Property}:{d.Value}"));
        if (string.IsNullOrWhiteSpace(raw))
            return added;
        if (raw.Contains('{') || raw.Contains('}') || raw.Contains('&'))
            return raw.TrimEnd().TrimEnd(';') + ";" + added;
        var names = declarations.Select(d => d.Property).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = raw.Split(';')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0 && !names.Contains(part.Split(':', 2)[0].Trim()));
        return string.Join(';', kept.Append(added));
    }
}
