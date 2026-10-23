namespace XsltCraft.Application.Validation.Safety;

/// <summary>
/// XsltSafety tarama modu — hangi bulgu sınıflarının <b>engel</b> sayılacağını belirler.
/// </summary>
public enum XsltSafetyMode
{
    /// <summary>
    /// İçerik render motoruna gidecek (generator Validate, ham XSLT çalıştırma, worker).
    /// Hem <see cref="XsltSafetySeverity.Security"/> hem <see cref="XsltSafetySeverity.Unsupported"/>
    /// bulguları engeldir — XSLT 2.0 tavanı zorlanır.
    /// </summary>
    Execute,

    /// <summary>
    /// İçerik yalnız saklanacak / analiz edilecek (tema yükleme, admin kaydı, slot analizi).
    /// Yalnız <see cref="XsltSafetySeverity.Security"/> bulguları engeldir; desteklenmeyen 3.0
    /// yapıları bu aşamada reddedilmez (çalıştırma anında Execute ile yakalanır).
    /// </summary>
    Store,
}
