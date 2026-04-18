namespace XsltCraft.Application.Validation;

public interface IUblTrBusinessRuleService
{
    /// <summary>
    /// UBL-TR Fatura XML'ini ~15 iş kuralına göre doğrular.
    /// Geçersiz XML durumunda tek bir Error sonucu döner.
    /// </summary>
    IReadOnlyList<BusinessRuleResult> Validate(string xmlContent);
}
