using System.Text;

using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

/// <summary>
/// Sağlayıcıya özel bağlam bütçesi: büyük pencereli sağlayıcı (Gemini) TAM ham XSLT alır,
/// küçük pencereli (Ollama/varsayılan) özetlenmiş XSLT alır. "Auto" yönlendirme kuralları.
/// </summary>
public class ContextBudgetTests
{
    /// <summary>~12K karakterlik, özet eşiğinin (6K) üzerinde sentetik stylesheet üretir.</summary>
    private static string BuildLargeXslt()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\"?>\n");
        sb.Append("<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" ");
        sb.Append("xmlns:n1=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" ");
        sb.Append("xmlns:cbc=\"urn:oasis:names:tc:ubl:schema:xsd:CommonBasicComponents-2\">\n");
        for (var i = 0; i < 40; i++)
        {
            sb.Append($"<xsl:template match=\"n1:Alan{i}\">\n");
            sb.Append($"  <div class=\"alan-{i}\"><xsl:value-of select=\"cbc:Deger{i}\"/></div>\n");
            sb.Append("  <!-- ").Append(new string('x', 200)).Append(" -->\n");
            sb.Append("</xsl:template>\n");
        }
        sb.Append("</xsl:stylesheet>");
        return sb.ToString();
    }

    private static AiRequest Req(string xslt) => new()
    {
        Task = AiTaskKind.Assistant,
        UserRequest = "Alan7 içindeki cbc:Deger7 alanını kaldır",
        UserXslt = xslt,
        History = [],
    };

    private static string FirstUserContent(IReadOnlyList<ProviderMessage> messages)
        => messages.First(m => m.Role == "user").Content;

    [Fact]
    public void Default_budget_summarizes_large_xslt()
    {
        var xslt = BuildLargeXslt();
        Assert.True(xslt.Length > 6_000); // özet eşiğinin üzerinde olmalı

        var messages = PromptTemplates.BuildAssistant(Req(xslt));

        var ctx = FirstUserContent(messages);
        // Özetleyici imzası: TEMPLATE INDEX bölümü.
        Assert.Contains("TEMPLATE INDEX", ctx);
        // Tam dosya gönderilmedi.
        Assert.DoesNotContain(xslt, ctx);
    }

    [Fact]
    public void Large_budget_sends_full_raw_xslt()
    {
        var xslt = BuildLargeXslt();
        var budget = new AiContextBudget(
            RawXsltThresholdChars: 400_000, XsltLimitChars: 400_000, XmlLimitChars: 100_000);

        var messages = PromptTemplates.BuildAssistant(Req(xslt), budget);

        var ctx = FirstUserContent(messages);
        // TAM dosya, özetsiz ve kırpılmasız gönderildi.
        Assert.Contains(xslt, ctx);
        Assert.DoesNotContain("TEMPLATE INDEX", ctx);
        Assert.DoesNotContain("[kırpıldı]", ctx);
    }

    [Fact]
    public void Large_budget_clips_when_over_limit()
    {
        var xslt = BuildLargeXslt();
        // Ham eşik yüksek ama limit küçük: ham başlar, Clip güvenlik ağı devrede.
        var budget = new AiContextBudget(
            RawXsltThresholdChars: 400_000, XsltLimitChars: 5_000, XmlLimitChars: 1_000);

        var messages = PromptTemplates.BuildAssistant(Req(xslt), budget);

        var ctx = FirstUserContent(messages);
        Assert.Contains("[kırpıldı]", ctx);
    }

    // ── Auto yönlendirme ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("gemini", AiTaskKind.Assistant, 1_000, "gemini")]   // açık tercih kazanır
    [InlineData("ollama", AiTaskKind.Assistant, 999_999, "ollama")] // açık tercih kazanır (büyük dosyada bile)
    [InlineData("auto", AiTaskKind.Assistant, 250_000, "gemini")]   // büyük XSLT → Gemini
    [InlineData("auto", AiTaskKind.Assistant, 10_000, "ollama")]    // küçük XSLT → Ollama
    [InlineData("auto", AiTaskKind.RefactorSelection, 250_000, "ollama")] // yalnız Assistant yönlenir
    public void Routing_resolves_expected_provider(string preferred, AiTaskKind task, int xsltLen, string expected)
    {
        Assert.Equal(expected, ProviderRouting.Resolve(preferred, task, xsltLen, largeXsltThresholdChars: 60_000));
    }

    [Fact]
    public void Routing_disabled_when_threshold_zero()
    {
        Assert.Equal("ollama", ProviderRouting.Resolve("auto", AiTaskKind.Assistant, 999_999, 0));
    }
}
