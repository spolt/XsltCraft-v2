using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using XsltCraft.Api.Xslt;
using XsltCraft.Application.XPath;

namespace XsltCraft.Controllers;

[ApiController]
[Route("api/xpath")]
public class XPathController : ControllerBase
{
    private readonly IXPathEvaluator _evaluator;

    public XPathController(IXPathEvaluator evaluator) => _evaluator = evaluator;

    /// <summary>
    /// XPath konsolu (G4): yetkili + kullanıcı başı 120/dk; maliyet kapısı reddi → 400 <c>{ kind:"error", error, code }</c>
    /// (<c>XPATH_TOO_LONG</c>/<c>XPATH_TOO_COMPLEX</c>), meşgul → 503 <c>XPATH_BUSY</c>; sözdizimi/XML/süre hataları
    /// önceki sözleşmedeki gibi 200 <c>kind:"error"</c> (+ <c>code</c>).
    /// </summary>
    [HttpPost("evaluate"), Authorize]
    [EnableRateLimiting(RateLimitPolicies.XPathEvaluate)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Evaluate([FromBody] XPathEvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.XpathExpression))
            return BadRequest(new { error = "XPath ifadesi boş olamaz." });

        if (string.IsNullOrWhiteSpace(request.XmlContent))
            return BadRequest(new { error = "XML içeriği boş olamaz." });

        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        var ct = HttpContext.RequestAborted;
        XPathEvaluateResponse result;
        try
        {
            result = await _evaluator.EvaluateAsync(request.XpathExpression, request.XmlContent, userId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }

        if (XPathEvaluator.IsRejection(result.Code))
            return BadRequest(result);
        if (result.Code == XPathEvaluator.Busy)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        return Ok(result);
    }
}
