namespace XsltCraft.Application.Validation;

public enum BusinessRuleSeverity
{
    Error,
    Warning,
    Info
}

public record BusinessRuleResult(
    string RuleId,
    string RuleName,
    BusinessRuleSeverity Severity,
    string Message,
    int? Line = null,
    int? Column = null,
    string? Xpath = null);
