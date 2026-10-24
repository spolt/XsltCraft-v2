using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary><see cref="XsltSafety"/>'yi enjekte edilebilir (testte sahtelenebilir) yapan ince arayüz.</summary>
public interface IXsltSafetyScanner
{
    XsltSafetyReport Scan(string? xslt, XsltSafetyMode mode);
}

/// <summary>Varsayılan uygulama: doğrudan <see cref="XsltSafety.Scan"/>.</summary>
public sealed class DefaultXsltSafetyScanner : IXsltSafetyScanner
{
    public XsltSafetyReport Scan(string? xslt, XsltSafetyMode mode) => XsltSafety.Scan(xslt, mode);
}
