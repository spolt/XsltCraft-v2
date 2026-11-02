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

    /// <summary>
    /// <paramref name="ex"/>'in iç istisna zincirindeki ilk <see cref="XsltProcessingException"/>
    /// (yazıcı/okuyucudan fırlayıp motor tarafından sarılmış sınır hataları için); yoksa <c>null</c>.
    /// </summary>
    public static XsltProcessingException? FindIn(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        for (Exception? cur = ex.InnerException; cur != null; cur = cur.InnerException)
        {
            if (cur is XsltProcessingException xpe)
                return xpe;
        }

        return null;
    }
}
