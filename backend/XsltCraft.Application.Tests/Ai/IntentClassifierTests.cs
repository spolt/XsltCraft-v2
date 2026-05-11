using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class IntentClassifierTests
{
    [Theory]
    [InlineData("selam")]
    [InlineData("Selam")]
    [InlineData("merhaba")]
    [InlineData("teşekkürler")]
    [InlineData("sağol")]
    [InlineData("eyvallah")]
    [InlineData("kimsin")]
    [InlineData("kim sin")]
    [InlineData("naber")]
    [InlineData("hi")]
    [InlineData("hello")]
    [InlineData("iyi günler")]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_smalltalk(string input)
    {
        var req = new AiRequest { Task = AiTaskKind.Assistant, UserRequest = input };
        Assert.Equal(AiIntent.Smalltalk, IntentClassifier.Classify(req));
    }

    [Theory]
    [InlineData("XSLT nedir?")]
    [InlineData("XPath nasıl çalışır")]
    [InlineData("UBL fatura ne işe yarar?")]
    [InlineData("namespace nedir")]
    [InlineData("e-arşiv ile e-fatura farkı ne?")]
    public void Classify_general(string input)
    {
        var req = new AiRequest { Task = AiTaskKind.Assistant, UserRequest = input };
        Assert.Equal(AiIntent.General, IntentClassifier.Classify(req));
    }

    [Theory]
    [InlineData("bu XSLT'de hata var")]
    [InlineData("burada neden çalışmıyor")]
    [InlineData("şu satırı düzelt")]
    [InlineData("note prefix gizle ETF")]
    [InlineData("<xsl:for-each select=\"//x\"/>")]
    [InlineData("`xsl:value-of` nasıl kullanılır")]
    [InlineData("xsl:template match nasıl yazılır")]
    [InlineData("cbc:Note bloğunu kaldır")]
    [InlineData("bu kodda optimize edilecek yer var mı")]
    public void Classify_code(string input)
    {
        var req = new AiRequest { Task = AiTaskKind.Assistant, UserRequest = input };
        Assert.Equal(AiIntent.Code, IntentClassifier.Classify(req));
    }

    [Fact]
    public void Refactor_task_is_always_code()
    {
        var req = new AiRequest { Task = AiTaskKind.RefactorSelection, UserRequest = "selam" };
        Assert.Equal(AiIntent.Code, IntentClassifier.Classify(req));
    }

    [Fact]
    public void XmlSelection_forces_code_intent()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserRequest = "selam",
            XmlSelection = "<Invoice/>",
        };
        Assert.Equal(AiIntent.Code, IntentClassifier.Classify(req));
    }

    [Fact]
    public void Selection_forces_code_intent()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserRequest = "selam",
            Selection = "<xsl:for-each/>",
        };
        Assert.Equal(AiIntent.Code, IntentClassifier.Classify(req));
    }

    [Fact]
    public void Long_unclear_message_defaults_to_code()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserRequest = "bana yardım edebilecek misin acaba bilemiyorum bir şeyler yapalım ne dersin",
        };
        Assert.Equal(AiIntent.Code, IntentClassifier.Classify(req));
    }
}
