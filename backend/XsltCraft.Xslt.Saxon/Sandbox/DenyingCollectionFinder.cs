using net.sf.saxon.expr;
using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary><c>collection()</c> / <c>uri-collection()</c> reddi.</summary>
internal sealed class DenyingCollectionFinder : CollectionFinder
{
    public static readonly DenyingCollectionFinder Instance = new();

    public ResourceCollection findCollection(XPathContext context, string collectionURI) =>
        throw new SandboxViolationException(SandboxCodes.CollectionDenied, "collection() / uri-collection() yasak.");
}
