using net.sf.saxon;
using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary><c>unparsed-text()</c>, <c>unparsed-text-lines()</c>, <c>json-doc()</c> reddi.</summary>
internal sealed class DenyingUnparsedTextResolver : UnparsedTextURIResolver
{
    public static readonly DenyingUnparsedTextResolver Instance = new();

    public java.io.Reader resolve(java.net.URI absoluteURI, string encoding, Configuration config) =>
        throw new SandboxViolationException(SandboxCodes.UnparsedTextDenied, "Metin dosyası okuma yasak (unparsed-text/json-doc).");
}
