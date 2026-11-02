using System.Diagnostics;
using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// <see cref="DotNetXsltEngine"/> derleme sonucu. Girdi yükleme ve dönüşüm
/// <see cref="XsltHtmlRenderer"/> ile aynıdır (çıktı bit-aynı); çıktı <see cref="LimitedTextWriter"/>
/// ile sınırlanır (süre + <see cref="XsltExecutionLimits.Cancellation"/> her yazımda), girdi derinlik/düğüm sınırları DOM kurulurken aynı okuma geçişinde
/// (<see cref="LimitedXmlReader"/>) denetlenir.
/// </summary>
internal sealed class DotNetCompiledXslt(XslCompiledTransform transform) : ICompiledXslt
{
    private const XsltEngineKind Engine = XsltEngineKind.DotNet;

    public XsltEngineKind Kind => Engine;

    public XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(limits);

        var sw = Stopwatch.StartNew();
        var xmlDoc = LoadInput(xml, limits);
        var parseMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var limited = new LimitedTextWriter(output, limits.MaxOutputChars, limits.Deadline, Engine, limits.Cancellation);
        Run(xmlDoc, limited);
        var transformMs = sw.ElapsedMilliseconds;

        sw.Restart();
        limited.Flush();
        var serializeMs = sw.ElapsedMilliseconds;

        return new XsltTransformTimings(parseMs, CompileMs: 0, transformMs, serializeMs);
    }

    private static XmlDocument LoadInput(string xml, XsltExecutionLimits limits)
    {
        try
        {
            // XsltHtmlRenderer ile aynı yükleme — XmlDocument'in boşluk davranışı çıktıyı etkiler.
            var xmlDoc = new XmlDocument { XmlResolver = null };
            using var reader = new LimitedXmlReader(
                XmlReader.Create(new StringReader(xml), SecureXslt.ReaderSettings()),
                limits.MaxInputDepth, limits.MaxInputNodes, XsltFailureKind.Xml, Engine, "XML");
            xmlDoc.Load(reader);
            return xmlDoc;
        }
        catch (XmlException ex)
        {
            throw new XsltProcessingException(XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Xml, Engine), ex);
        }
    }

    private void Run(XmlDocument xmlDoc, TextWriter writer)
    {
        try
        {
            transform.Transform(xmlDoc, arguments: null, writer);
        }
        catch (XsltProcessingException)
        {
            throw;
        }
        catch (Exception ex) when (XsltProcessingException.FindIn(ex) is { } inner)
        {
            // Yazıcıdan fırlayan sınır istisnası XslCompiledTransform tarafından sarılmış olabilir.
            throw new XsltProcessingException(inner.Diagnostic, ex);
        }
        catch (XsltException ex)
        {
            throw new XsltProcessingException(XsltDiagnostics.FromXsltException(ex, XsltFailureKind.Runtime, Engine), ex);
        }
        catch (XmlException ex)
        {
            throw new XsltProcessingException(XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Runtime, Engine), ex);
        }
    }
}
