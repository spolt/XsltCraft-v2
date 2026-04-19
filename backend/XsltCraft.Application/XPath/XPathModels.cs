namespace XsltCraft.Application.XPath;

public record XPathEvaluateRequest(string XpathExpression, string XmlContent);

public record XPathResultItem(
    string Kind,
    string Name,
    string Value,
    int? Line,
    int? Column
);

public record XPathEvaluateResponse(
    string Kind,
    IReadOnlyList<XPathResultItem> Items,
    long ExecutionMs,
    string? Error
);

public interface IXPathEvaluator
{
    XPathEvaluateResponse Evaluate(string xpathExpression, string xmlContent);
}
