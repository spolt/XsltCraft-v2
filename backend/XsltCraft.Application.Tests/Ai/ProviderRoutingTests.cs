using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class ProviderRoutingTests
{
    [Theory]
    [InlineData("gemini", AiTaskKind.Assistant, 0, "gemini")]
    [InlineData("ollama", AiTaskKind.Assistant, 999_999, "ollama")]          // açık tercih büyük XSLT'yi ezer
    [InlineData("auto", AiTaskKind.Assistant, 100, "ollama")]
    [InlineData("auto", AiTaskKind.Assistant, 64_001, "gemini")]             // büyük XSLT → Gemini
    [InlineData("auto", AiTaskKind.RefactorSelection, 64_001, "ollama")]     // kural yalnız assistant
    [InlineData("bilinmeyen", AiTaskKind.Assistant, 64_001, "gemini")]       // bilinmeyen = auto
    public void Resolve_TextRouting(string preferred, AiTaskKind task, int xsltLength, string expected)
        => Assert.Equal(expected, ProviderRouting.Resolve(preferred, task, xsltLength, 64_000));

    [Fact]
    public void Resolve_ThresholdZero_DisablesLargeXsltRule()
        => Assert.Equal("ollama", ProviderRouting.Resolve("auto", AiTaskKind.Assistant, 1_000_000, 0));

    [Theory]
    // auto / gemini / bilinmeyen: Gemini önce, yerel vision yedek
    [InlineData("auto", true, true, false, "gemini,ollama")]
    [InlineData("auto", false, true, false, "gemini")]
    [InlineData("auto", true, false, false, "ollama")]
    [InlineData("auto", false, false, true, "")]
    [InlineData("gemini", true, true, false, "gemini,ollama")]
    [InlineData("bilinmeyen", true, true, true, "gemini,ollama")]
    // ollama: yerel önce; Gemini yalnız istisna (fallback) açıksa
    [InlineData("ollama", true, true, true, "ollama,gemini")]
    [InlineData("ollama", true, true, false, "ollama")]
    [InlineData("ollama", false, true, true, "gemini")]       // yerel model yok, istisna açık → Gemini
    [InlineData("ollama", false, true, false, "")]            // katı yerel, model yok → kullanılamaz
    [InlineData("ollama", false, false, true, "")]
    public void ResolveVision_Matrix(string preferred, bool ollamaVision, bool geminiVision, bool geminiFallback, string expected)
    {
        var result = ProviderRouting.ResolveVision(preferred, ollamaVision, geminiVision, geminiFallback);
        Assert.Equal(expected, string.Join(',', result));
    }
}
