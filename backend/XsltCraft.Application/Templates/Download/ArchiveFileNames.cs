using System.Globalization;
using System.Text;

namespace XsltCraft.Application.Templates.Download;

/// <summary>
/// ZIP girdi/dosya adı temizleme (Zip Slip ve Windows uyumluluğu). Saf ve durumsuz.
/// <list type="bullet">
/// <item>Yasak karakterler (<c>/ \ : * ? " &lt; &gt; |</c>), Unicode Cc/Cf ve eşsiz vekil karakterleri → <c>_</c>.</item>
/// <item><c>..</c> dizileri tek noktaya iner; baştaki/sondaki nokta ve boşluk kırpılır — dizin bileşeni asla oluşmaz.</item>
/// <item>Gövde (ilk noktadan önceki kısım) Windows ayrılmış adıysa (<c>CON</c>, <c>COM1</c>…) <c>_</c> önek alır.</item>
/// <item>Gövde en fazla <see cref="MaxStemLength"/> karakter (vekil çifti bölünmez); boş kalırsa <see cref="FallbackStem"/>.</item>
/// <item>Girdi adı <c>.xslt</c>/<c>.xsl</c> ile bitmiyorsa <c>.xslt</c> eklenir.</item>
/// </list>
/// </summary>
public static class ArchiveFileNames
{
    public const int MaxStemLength = 120;
    public const string FallbackStem = "sablon";
    public const string DefaultExtension = ".xslt";

    private static readonly string[] KeptExtensions = [".xslt", ".xsl"];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
        "CONIN$", "CONOUT$",
    };

    /// <summary>Şablon adından uzantısız gövde ve uzantıyı üretir (tekilleştirme öncesi).</summary>
    public static (string Stem, string Extension) SplitEntryName(string? name)
    {
        var raw = name ?? string.Empty;
        var trimmed = raw.TrimEnd(' ', '.');
        foreach (var ext in KeptExtensions)
        {
            if (trimmed.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                var baseName = trimmed[..^ext.Length];
                return (SanitizeStem(baseName), trimmed[^ext.Length..]);
            }
        }
        return (SanitizeStem(raw), DefaultExtension);
    }

    /// <summary>Şablon adından tekilleştirilmemiş güvenli girdi adı (ör. <c>Fatura.xslt</c>).</summary>
    public static string ToEntryName(string? name)
    {
        var (stem, ext) = SplitEntryName(name);
        return stem + ext;
    }

    /// <summary>
    /// Uzantısız güvenli dosya adı gövdesi. Klasör ZIP adı (<c>{gövde}.zip</c>) için de kullanılır.
    /// Boş kalırsa <paramref name="fallback"/> döner.
    /// </summary>
    public static string SanitizeStem(string? name, string fallback = FallbackStem)
    {
        var stem = ReplaceInvalidChars(name ?? string.Empty);
        stem = CollapseDotRuns(stem);
        stem = TrimDotsAndSpaces(stem);
        stem = TrimDotsAndSpaces(Truncate(stem, MaxStemLength));

        if (stem.Length == 0)
            return fallback;

        if (IsReserved(stem))
        {
            stem = "_" + stem;
            if (stem.Length > MaxStemLength)
                stem = TrimDotsAndSpaces(Truncate(stem, MaxStemLength));
        }

        return stem;
    }

    private static string ReplaceInvalidChars(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                sb.Append(c).Append(value[i + 1]);
                i++;
                continue;
            }
            sb.Append(IsInvalidChar(c) ? '_' : c);
        }
        return sb.ToString();
    }

    private static bool IsInvalidChar(char c)
    {
        switch (c)
        {
            case '/':
            case '\\':
            case ':':
            case '*':
            case '?':
            case '"':
            case '<':
            case '>':
            case '|':
                return true;
        }
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate;
    }

    private static string CollapseDotRuns(string value)
    {
        while (value.Contains("..", StringComparison.Ordinal))
            value = value.Replace("..", ".", StringComparison.Ordinal);
        return value;
    }

    private static string TrimDotsAndSpaces(string value) => value.Trim(' ', '.');

    /// <summary>En fazla <paramref name="max"/> UTF-16 birimi; vekil çiftinin ortasından kesmez.</summary>
    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;
        var cut = max;
        if (char.IsHighSurrogate(value[cut - 1]))
            cut--;
        return value[..cut];
    }

    /// <summary>İlk noktadan önceki gövde (sondaki boşluklar yok sayılarak) Windows ayrılmış adı mı?</summary>
    private static bool IsReserved(string stem)
    {
        var dot = stem.IndexOf('.');
        var body = (dot >= 0 ? stem[..dot] : stem).TrimEnd(' ');
        return ReservedNames.Contains(body);
    }
}

/// <summary>
/// Tek arşiv içinde girdi adlarını tekilleştirir (büyük/küçük harf duyarsız): <c>Ad.xslt</c>, <c>Ad (2).xslt</c>…
/// Durumludur; her arşiv için yeni örnek kullanılır.
/// </summary>
public sealed class ArchiveNameRegistry
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ham şablon adını temizler, tekil girdi adını ayırır ve döndürür.</summary>
    public string Reserve(string? rawName)
    {
        var (stem, ext) = ArchiveFileNames.SplitEntryName(rawName);
        var candidate = stem + ext;
        for (var n = 2; !_used.Add(candidate); n++)
            candidate = $"{stem} ({n.ToString(CultureInfo.InvariantCulture)}){ext}";
        return candidate;
    }
}
