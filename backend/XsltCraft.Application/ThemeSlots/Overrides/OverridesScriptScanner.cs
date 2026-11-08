using System.Text;

namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <param name="Scripts">Sırasıyla her <c>&lt;script …&gt;…&lt;/script&gt;</c> öğesinin nitelik metni ve gövdesi.</param>
/// <param name="Rest">Betikler çıkarıldıktan sonra kalan metin.</param>
internal readonly record struct OverridesScripts(IReadOnlyList<(string Attrs, string Body)> Scripts, string Rest);

/// <summary>
/// Görsel bloğundaki <c>script</c> öğelerini (büyük/küçük harf duyarsız) tek geçişte ayırır: nitelikler ilk
/// <c>&gt;</c>'a, gövde ilk <c>&lt;/script&gt;</c>'e kadar. Süre girdiyle doğrusal.
/// </summary>
internal static class OverridesScriptScanner
{
    private const string Open = "<script";
    private const string Close = "</script>";

    /// <summary>Kapanmayan bir betik varsa <c>null</c>.</summary>
    public static OverridesScripts? Scan(string text)
    {
        var scripts = new List<(string, string)>();
        var rest = new StringBuilder(text.Length);
        var pos = 0;
        var search = 0;
        while (true)
        {
            var start = text.IndexOf(Open, search, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;
            var afterName = start + Open.Length;
            if (afterName < text.Length && IsWordChar(text[afterName]))
            {
                search = afterName;
                continue;
            }

            var attrsEnd = text.IndexOf('>', afterName);
            var closeAt = attrsEnd < 0 ? -1 : text.IndexOf(Close, attrsEnd + 1, StringComparison.OrdinalIgnoreCase);
            if (closeAt < 0)
                return null;

            rest.Append(text, pos, start - pos);
            scripts.Add((text[afterName..attrsEnd], text[(attrsEnd + 1)..closeAt]));
            pos = search = closeAt + Close.Length;
        }

        rest.Append(text, pos, text.Length - pos);
        return new OverridesScripts(scripts, rest.ToString());
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
