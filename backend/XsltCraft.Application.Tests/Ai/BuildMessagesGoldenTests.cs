using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class BuildMessagesGoldenTests
{
    private const string SimpleXslt =
        "<?xml version=\"1.0\"?>" +
        "<xsl:stylesheet version=\"2.0\" " +
        "xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" " +
        "xmlns:n1=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" " +
        "xmlns:cbc=\"urn:oasis:names:tc:ubl:schema:xsd:CommonBasicComponents-2\" " +
        "xmlns:cac=\"urn:oasis:names:tc:ubl:schema:xsd:CommonAggregateComponents-2\">" +
        "<xsl:for-each select=\"//n1:Invoice/cbc:Note\"/>" +
        "</xsl:stylesheet>";

    [Fact]
    public Task Refactor_RefactorSelection()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.RefactorSelection,
            UserRequest = "note prefix gizle ETF",
            UserXslt = SimpleXslt,
            UserXml = "<Invoice><cbc:Note>ETF:100</cbc:Note></Invoice>",
            Selection = "<xsl:for-each select=\"//n1:Invoice/cbc:Note\">\n  <b>Not: </b><xsl:value-of select=\".\"/>\n</xsl:for-each>",
        };
        var messages = PromptTemplates.BuildMessages(req, AiMode.Refactor);
        return Verifier.Verify(messages).UseDirectory("__snapshots__");
    }

    [Fact]
    public Task Assistant_FirstTurn()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserRequest = "satıcı adresini sadece şehir ve ilçe göster",
            UserXslt = SimpleXslt,
            History = [],
        };
        var messages = PromptTemplates.BuildMessages(req, AiMode.Assistant);
        return Verifier.Verify(messages).UseDirectory("__snapshots__");
    }

    [Fact]
    public Task Assistant_ThirdTurn_WithHistory()
    {
        var req = new AiRequest
        {
            Task = AiTaskKind.Assistant,
            UserRequest = "peki telefon numarasını da gizle",
            UserXslt = SimpleXslt,
            History =
            [
                new AssistantMessage("user",      "satıcı adresini sadece şehir ve ilçe göster"),
                new AssistantMessage("assistant", "Tamam, şu değişikliği yapın: PostalAddress bloğundan StreetName/BuildingName/BuildingNumber/Room kaldırın."),
                new AssistantMessage("user",      "faks numarası da gizlensin"),
                new AssistantMessage("assistant", "Faks için xsl:if içinden cbc:Telefax bloğunu ve 'or ...Telefax' koşulunu silin."),
            ],
        };
        var messages = PromptTemplates.BuildMessages(req, AiMode.Assistant);
        return Verifier.Verify(messages).UseDirectory("__snapshots__");
    }
}
