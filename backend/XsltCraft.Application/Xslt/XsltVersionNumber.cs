using System.Globalization;

namespace XsltCraft.Application.Xslt;

/// <summary>
/// XSLT <c>version</c> öznitelik değerini ayrıştırmanın tek yolu — motor seçimi
/// (<see cref="Engines.XsltVersionSniffer"/>) ve güvenlik taraması (XsltSafetyScanner) aynı sonucu
/// görsün diye paylaşılır. Kural: kenar boşlukları kırpılır; yalnız basamak ve ondalık nokta
/// (değişmez kültür) kabul edilir — işaret, binlik ayıracı, üs ve virgül reddedilir.
/// </summary>
public static class XsltVersionNumber
{
    public static bool TryParse(string? value, out decimal version) =>
        decimal.TryParse(value?.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out version);

    public static decimal? Parse(string? value) =>
        TryParse(value, out var version) ? version : null;
}
