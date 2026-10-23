using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// .NET <see cref="XslCompiledTransform"/> (XSLT 1.0) motoru. Derleme yalnız
/// <see cref="SecureXslt.Compile"/> üzerinden; hatalar <see cref="XsltProcessingException"/>.
/// Stylesheet eleman derinliği <see cref="XsltExecutionLimits.MaxStylesheetDepth"/> ile sınırlanır.
/// </summary>
public sealed class DotNetXsltEngine : IXsltEngine
{
    public XsltEngineKind Kind => XsltEngineKind.DotNet;

    public ICompiledXslt Compile(string xslt, XsltExecutionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(xslt);
        ArgumentNullException.ThrowIfNull(limits);
        try
        {
            XmlShapeGuard.Ensure(xslt, limits.MaxStylesheetDepth, int.MaxValue,
                XsltFailureKind.LimitExceeded, XsltEngineKind.DotNet, "XSLT");
            return new DotNetCompiledXslt(SecureXslt.Compile(xslt));
        }
        catch (XsltException ex)
        {
            throw new XsltProcessingException(
                XsltDiagnostics.FromXsltException(ex, XsltFailureKind.Compile, XsltEngineKind.DotNet), ex);
        }
        catch (XmlException ex)
        {
            throw new XsltProcessingException(
                XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Compile, XsltEngineKind.DotNet), ex);
        }
    }
}
