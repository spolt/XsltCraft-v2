namespace XsltCraft.Application.XPath;

public record XPathEvaluateRequest(string XpathExpression, string XmlContent);

public record XPathResultItem(
    string Kind,        // element | attribute | text | comment | pi | atomic | node
    string Name,        // element/attribute adı (prefix:local)
    string Value,       // string değeri (max 200 karakter)
    int?   Line,
    int?   Column
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
