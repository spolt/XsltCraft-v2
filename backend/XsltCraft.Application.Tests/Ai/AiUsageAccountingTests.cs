using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class AiUsageAccountingTests
{
    private const int Cost = 1000;

    [Theory]
    // metin-only: her istek sayılır (mevcut davranış), çıktı yoksa token 0
    [InlineData(0, 0, false, true, 0)]
    [InlineData(0, 400, false, true, 101)]
    // görselli + çıktı: sayılır, görsel maliyeti eklenir
    [InlineData(2, 400, false, true, 101 + 2 * Cost)]
    // görselli + sağlayıcı çıktı üretmedi (model yok/hata): hak yanmaz
    [InlineData(1, 0, false, false, 0)]
    // görselli + istemci iptali (ilk token'dan önce): YİNE sayılır — kota atlatma kapalı
    [InlineData(1, 0, true, true, Cost)]
    public void Compute(int images, int outputChars, bool cancelled, bool expectedCount, int expectedTokens)
    {
        var (count, tokens) = AiUsageAccounting.Compute(images, outputChars, cancelled, Cost);

        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedTokens, tokens);
    }
}
