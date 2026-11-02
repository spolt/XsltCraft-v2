namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// <c>xsl:result-document</c> reddi (<c>Xslt30Transformer.setResultDocumentHandler</c>). Asıl engel derleme
/// anındadır (ALLOW_EXTERNAL_FUNCTIONS=false), bu çalışma anı ikinci katmandır.
/// </summary>
internal sealed class DenyingResultDocumentHandler : java.util.function.Function
{
    public static readonly DenyingResultDocumentHandler Instance = new();

    public object apply(object uri) =>
        throw new SandboxViolationException(SandboxCodes.ResultDocumentDenied, "xsl:result-document yasak.");

    // IKVM, Java default metotlarını C#'a görünür kılmaz; elle uygulanmalı.
    public java.util.function.Function andThen(java.util.function.Function after) => throw new NotSupportedException();

    public java.util.function.Function compose(java.util.function.Function before) => throw new NotSupportedException();
}
