namespace XsltCraft.Application.Xslt.Engines;

/// <summary>Motorların dışarıya fırlattığı tek hata tipi; ayrıntı <see cref="Diagnostic"/> içindedir.</summary>
public sealed class XsltProcessingException : Exception
{
    public XsltProcessingException(XsltDiagnostic diagnostic, Exception? innerException = null)
        : base(diagnostic.Message, innerException)
    {
        Diagnostic = diagnostic;
    }

    public XsltDiagnostic Diagnostic { get; }
}
