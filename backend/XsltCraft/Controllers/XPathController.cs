using Microsoft.AspNetCore.Mvc;
using XsltCraft.Application.XPath;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/xpath")]
public class XPathController : ControllerBase
{
    private readonly IXPathEvaluator _evaluator;

    public XPathController(IXPathEvaluator evaluator) => _evaluator = evaluator;

    [HttpPost("evaluate")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public IActionResult Evaluate([FromBody] XPathEvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.XpathExpression))
            return BadRequest(new { error = "XPath ifadesi boş olamaz." });

        if (string.IsNullOrWhiteSpace(request.XmlContent))
            return BadRequest(new { error = "XML içeriği boş olamaz." });

        var result = _evaluator.Evaluate(request.XpathExpression, request.XmlContent);
        return Ok(result);
    }
}
