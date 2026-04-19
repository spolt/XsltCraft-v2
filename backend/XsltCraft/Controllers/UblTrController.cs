using Microsoft.AspNetCore.Mvc;

using XsltCraft.Application.Validation;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/ubl-tr")]
public class UblTrController(IUblTrBusinessRuleService service) : ControllerBase
{
    private const int MaxXmlBytes = 2 * 1024 * 1024; // 2 MB

    /// <summary>
    /// UBL-TR fatura XML'ini ~15 iş kuralına göre doğrular.
    /// Request: { xmlContent }
    /// Response: { results: [{ ruleId, ruleName, severity, message, line?, column? }] }
    /// </summary>
    [HttpPost("validate-business-rules")]
    [RequestSizeLimit(MaxXmlBytes)]
    public IActionResult ValidateBusinessRules([FromBody] ValidateBusinessRulesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.XmlContent))
            return BadRequest(new { error = "XML içeriği boş olamaz." });

        if (System.Text.Encoding.UTF8.GetByteCount(request.XmlContent) > MaxXmlBytes)
            return BadRequest(new { error = "XML içeriği 2 MB'dan büyük olamaz." });

        var results = service.Validate(request.XmlContent);

        return Ok(new
        {
            results = results.Select(r => new
            {
                ruleId = r.RuleId,
                ruleName = r.RuleName,
                severity = r.Severity.ToString().ToLowerInvariant(),
                message = r.Message,
                line = r.Line,
                column = r.Column,
                xpath = r.Xpath
            }),
            summary = new
            {
                total = results.Count,
                errors = results.Count(r => r.Severity == BusinessRuleSeverity.Error),
                warnings = results.Count(r => r.Severity == BusinessRuleSeverity.Warning),
                infos = results.Count(r => r.Severity == BusinessRuleSeverity.Info)
            }
        });
    }
}

public class ValidateBusinessRulesRequest
{
    public string XmlContent { get; set; } = "";
}
