using System.Text.RegularExpressions;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Sabit notun Notlar alanındaki konumu: not döngüsünün hemen öncesi ya da Notlar hücresinin sonu.</summary>
public enum FixedNotePosition { Before, After }

public enum FixedNoteAlign { Left, Center, Right }

/// <summary>Tema Kullan "Sabit notlar": doğrulanmış (beyaz listeden geçmiş) not. Metin yalnız text node olarak yazılır.</summary>
public sealed record FixedNoteInput(
    FixedNotePosition Position,
    string Text,
    bool Bold,
    bool Italic,
    bool Underline,
    int FontSize,
    string Color,
    string FontFamily,
    FixedNoteAlign Align);

/// <summary>
/// Sabit not stil beyaz listesi — tek kaynak. <c>style</c> niteliği XSLT'de AVT olduğu için yalnız buradaki sabit
/// tablolardan üretilir; istemci metni hiçbir zaman CSS'e girmez.
/// </summary>
public static class FixedNoteStyles
{
    public const int MaxNotes = 2;
    public const int MaxTextLength = 500;
    public const int MaxLines = 10;

    public static readonly IReadOnlySet<int> FontSizes = new HashSet<int> { 9, 10, 11, 12, 13, 14, 16, 18 };

    public static readonly Regex ColorPattern = new(@"^#[0-9a-fA-F]{6}\z", RegexOptions.CultureInvariant);

    /// <summary>Yazı tipi anahtarı → web-güvenli yığın (<c>theme</c>: temanın yazı tipi, CSS yazılmaz).</summary>
    public static readonly IReadOnlyDictionary<string, string?> FontFamilies = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["theme"] = null,
        ["arial"] = "Arial,Helvetica,sans-serif",
        ["verdana"] = "Verdana,Geneva,sans-serif",
        ["tahoma"] = "Tahoma,Verdana,sans-serif",
        ["times"] = "'Times New Roman',Times,serif",
        ["georgia"] = "Georgia,'Times New Roman',serif",
        ["courier"] = "'Courier New',Courier,monospace",
    };

    public static bool TryParsePosition(string? value, out FixedNotePosition position)
    {
        (var ok, position) = value switch
        {
            "before" => (true, FixedNotePosition.Before),
            "after" => (true, FixedNotePosition.After),
            _ => (false, default),
        };
        return ok;
    }

    public static bool TryParseAlign(string? value, out FixedNoteAlign align)
    {
        (var ok, align) = value switch
        {
            "left" => (true, FixedNoteAlign.Left),
            "center" => (true, FixedNoteAlign.Center),
            "right" => (true, FixedNoteAlign.Right),
            _ => (false, default),
        };
        return ok;
    }

    public static string PositionValue(FixedNotePosition p) => p == FixedNotePosition.Before ? "before" : "after";

    /// <summary>Beyaz listeden CSS. Geçersiz değer (doğrulayıcıyı atlayan çağrı) → <see cref="ArgumentException"/>.</summary>
    public static string ToCss(FixedNoteInput n)
    {
        if (!FontSizes.Contains(n.FontSize))
            throw new ArgumentException("Geçersiz yazı boyutu.", nameof(n));
        if (!ColorPattern.IsMatch(n.Color))
            throw new ArgumentException("Geçersiz renk.", nameof(n));
        if (!FontFamilies.TryGetValue(n.FontFamily, out var family))
            throw new ArgumentException("Geçersiz yazı tipi.", nameof(n));

        var parts = new List<string> { "margin:2px 0", $"font-size:{n.FontSize}px", $"color:{n.Color.ToLowerInvariant()}" };
        if (family is not null)
            parts.Add($"font-family:{family}");
        parts.Add(n.Align switch
        {
            FixedNoteAlign.Center => "text-align:center",
            FixedNoteAlign.Right => "text-align:right",
            _ => "text-align:left",
        });
        if (n.Bold)
            parts.Add("font-weight:bold");
        if (n.Italic)
            parts.Add("font-style:italic");
        if (n.Underline)
            parts.Add("text-decoration:underline");
        // Kullanıcının baştaki/ardışık boşlukları HTML'de yutulmasın; uzun satırlar yine kaydırılır.
        parts.Add("white-space:pre-wrap");
        return string.Join(";", parts);
    }
}
