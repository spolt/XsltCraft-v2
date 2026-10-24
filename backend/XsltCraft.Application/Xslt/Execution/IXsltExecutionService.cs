namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// Kullanıcı kaynaklı XSLT çalıştırmanın tek girişi (ADR 007/008): boyut sınırı → XsltSafety
/// (Execute) → sürüm/motor seçimi → kullanıcı kapısı → rota başı host.
/// İçerik hataları <see cref="XsltRunResult.Diagnostic"/> olarak döner; yalnız iptal ve
/// beklenmeyen (programlama) hataları fırlar.
/// </summary>
public interface IXsltExecutionService
{
    Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default);
}
