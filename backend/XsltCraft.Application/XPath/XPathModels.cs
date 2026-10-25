namespace XsltCraft.Application.XPath;

public record XPathEvaluateRequest(string XpathExpression, string XmlContent);

public record XPathResultItem(
    string Kind,
    string Name,
    string Value,
    int? Line,
    int? Column
);

/// <param name="Code">
/// Hata sınıfı (<see cref="XPathEvaluator"/> sabitleri / <c>XPathPortability.TooLong|TooComplex</c>); başarıda <c>null</c>.
/// </param>
public record XPathEvaluateResponse(
    string Kind,
    IReadOnlyList<XPathResultItem> Items,
    long ExecutionMs,
    string? Error,
    string? Code = null
);

public interface IXPathEvaluator
{
    /// <summary>
    /// Serbest XPath 1.0 ifadesini XML'e karşı değerlendirir (XPath konsolu). Hiçbir zaman fırlatmaz
    /// (istemci iptali hariç); hatalar <c>Kind = "error"</c> + <see cref="XPathEvaluateResponse.Code"/> ile döner.
    /// </summary>
    Task<XPathEvaluateResponse> EvaluateAsync(string xpathExpression, string xmlContent, Guid userId,
        CancellationToken cancellationToken = default);
}
