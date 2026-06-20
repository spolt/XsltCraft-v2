using System.Text;

namespace XsltCraft.Application.Ai;

/// <summary>
/// Türkçe karakter folding + lowercase: ı/ş/ç/ğ/ü/ö → ASCII karşılıkları.
/// Niyet sınıflandırma, pattern seçimi ve template skorlamasının AYNI normalizasyonu
/// kullanması için tek kaynak. (Önceden IntentClassifier ve PatternSelector'da kopyaydı.)
/// </summary>
internal static class TextFold
{
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            sb.Append(c switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                'ü' => 'u',
                'ö' => 'o',
                _ => c,
            });
        }
        return sb.ToString();
    }
}
