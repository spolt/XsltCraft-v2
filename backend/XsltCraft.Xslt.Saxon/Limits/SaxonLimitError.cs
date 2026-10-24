using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Limits;

/// <summary>
/// Kaynak sınırı ihlali. BİLEREK <c>java.lang.Error</c> türevidir, <c>XPathException</c> DEĞİL:
/// Saxon'un <c>xsl:try</c>/<c>xsl:catch</c> uygulaması yalnız XPathException ailesini yakalar, bu yüzden
/// stylesheet bu hatayı yakalayıp özyinelemeye devam edemez (G0 K-1).
/// </summary>
public sealed class SaxonLimitError : java.lang.Error
{
    public SaxonLimitError(XsltFailureKind kind, string code, string message)
        : base(message)
    {
        Kind = kind;
        Code = code;
        UserMessage = message;
    }

    public XsltFailureKind Kind { get; }

    /// <summary><see cref="LimitCodes"/> değeri.</summary>
    public string Code { get; }

    public string UserMessage { get; }
}

/// <summary>Sınır ihlali kodları (<c>XsltDiagnostic.ErrorCode</c>).</summary>
public static class LimitCodes
{
    public const string Depth = "LimitTraceDepth";
    public const string Deadline = "LimitDeadline";
    public const string Cancelled = "LimitCancelled";
    public const string Output = "LimitOutputChars";
    public const string InputDepth = "LimitInputDepth";
    public const string InputNodes = "LimitInputNodes";
    public const string InputChars = "LimitInputChars";
    public const string StylesheetDepth = "LimitStylesheetDepth";
    public const string StylesheetChars = "LimitStylesheetChars";
    public const string Regex = "LimitRegexBacktracking";
}
