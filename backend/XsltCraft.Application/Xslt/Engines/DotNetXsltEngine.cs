using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// .NET <see cref="XslCompiledTransform"/> (XSLT 1.0) motoru. Derleme yalnız
/// <see cref="SecureXslt.CompileWithDepthLimit"/> üzerinden; hatalar <see cref="XsltProcessingException"/>.
/// Stylesheet eleman derinliği <see cref="XsltExecutionLimits.MaxStylesheetDepth"/> ile derleme okumasının
/// içinde (tek geçiş) sınırlanır.
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
            return new DotNetCompiledXslt(SecureXslt.CompileWithDepthLimit(xslt, limits.MaxStylesheetDepth));
        }
        catch (XsltProcessingException)
        {
            throw;
        }
        catch (Exception ex) when (XsltProcessingException.FindIn(ex) is { } inner)
        {
            // Okuyucudan fırlayan sınır istisnası XslCompiledTransform.Load tarafından sarılmış olabilir.
            throw new XsltProcessingException(inner.Diagnostic, ex);
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
