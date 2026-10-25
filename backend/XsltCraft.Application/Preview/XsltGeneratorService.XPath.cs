namespace XsltCraft.Application.Preview;

// Kullanıcı bağlamalı XPath'ler için çift-motor (.NET XSLT 1.0 / Saxon XSLT 2.0) güvenli yardımcılar (A5 G1–G3).
public sealed partial class XsltGeneratorService
{
    /// <summary>Taşınabilir olmayan kullanıcı XPath'i — Generate*() fail-closed <c>(null, Message)</c> döner.</summary>
    private sealed class XPathBindingException(string message) : Exception(message);

    /// <summary>
    /// Kullanıcı XPath'ini <see cref="XPathPortability"/> ile doğrular; taşınabilir değilse
    /// <see cref="XPathBindingException"/> fırlatır. Boş XPath dokunulmadan geçer (mevcut davranış:
    /// stylesheet derlemesi reddeder). <c>=</c>/<c>!=</c>, <c>test</c> EBV ve <c>for-each</c> için.
    /// </summary>
    private static string Bind(BlockDto block, string? xpath)
    {
        Check(block, xpath);
        return xpath ?? string.Empty;
    }

    /// <summary>
    /// Tekil değer bağlamı (<c>value-of</c>, <c>number()</c>, <c>string-length()</c>, <c>contains()</c>,
    /// <c>format-number()</c>): düğüm kümesi ise <c>(x)[1]</c> — XPath 1.0'ın "ilk düğüm" anlamı Saxon'da da korunur
    /// (2.0'da <c>value-of</c> tüm değerleri birleştirir, tekil-argüman fonksiyonları hata verir).
    /// </summary>
    private static string Single(BlockDto block, string? xpath)
    {
        var result = Check(block, xpath);
        return result is { IsNodeSet: true } ? $"({xpath})[1]" : xpath ?? string.Empty;
    }

    /// <summary>
    /// A5 G5 — <c>format-number()</c> öncesi motor-bağımsız ön-yuvarlama: <c>round(x * 10^d + 1e-6) div 10^d</c>.
    /// <para>Neden: <c>format-number</c> kırpmayı motor kendi kuralıyla yapar — .NET yarıyı sıfırdan uzağa
    /// (0,125 → 0,13), Saxon/XPath 2.0 yarıyı çifte (0,125 → 0,12). Ön-yuvarlanmış değer d. basamakta kesin
    /// (bağ yok) olduğundan iki motor aynı metni yazar. <c>round()</c> iki motorda aynıdır (yarı → +∞).</para>
    /// <para>Ölçekli alandaki <c>+1e-6</c> ikili kayan nokta artığını telafi eder (1,005 × 100 = 100,4999…; yalın
    /// <c>round(x*100)</c> 1,00 verirdi): böylece ondalık gösterimdeki bağlar yukarı yuvarlanır (1,005 → 1,01,
    /// 2,675 → 2,68) — mevcut .NET davranışı korunur. Bedeli: bir bağın 1e-8'inden (d=2) az altındaki değerler
    /// yukarı yuvarlanır; negatif bağlar +∞'a (−0,125 → −0,12) gider.</para>
    /// </summary>
    private static string PreRound(string numberExpr, int decimals)
    {
        var scale = decimals switch
        {
            2 => "100",
            3 => "1000",
            _ => throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Yalnız 2 ya da 3 ondalık."),
        };
        return $"round({numberExpr} * {scale} + 0.000001) div {scale}";
    }

    /// <summary>Generator'ın kendi ürettiği yol ifadesi (her zaman düğüm kümesi) için ilk düğüm.</summary>
    private static string FirstOf(string pathXpath) => $"({pathXpath})[1]";

    private static XPathPortabilityResult? Check(BlockDto block, string? xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath)) return null;
        var result = XPathPortability.Analyze(xpath);
        if (!result.IsPortable)
            throw new XPathBindingException(
                $"{result.ErrorCode}: '{xpath}' — {block.Type} bloğunda ({block.Id}) XPath kullanılamaz: {result.Message}");
        return result;
    }
}
