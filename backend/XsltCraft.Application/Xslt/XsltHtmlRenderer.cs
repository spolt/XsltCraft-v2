using System.Diagnostics;
using System.Xml;

namespace XsltCraft.Application.Xslt;

public sealed record XsltRenderResult(string Html, long ParseMs, long CompileMs, long TransformMs, long SerializeMs)
{
    public long TotalMs => ParseMs + CompileMs + TransformMs + SerializeMs;
}

/// <summary>
/// Ham XSLT'yi .NET (XSLT 1.0) ile XML'e uygulayıp HTML üretir — korumasız, senkron başvuru uygulaması: testlerde
/// bit-aynılık referansıdır (süreç içi .NET host çıktısı bununla aynıdır). Üretimde DI'da YOK — tema hattı dahil tüm
/// yürütme <c>IXsltExecutionService</c>'ten geçer (T-izi). Derleme <see cref="SecureXslt.Compile"/>; XML okuma XXE korumalı.
/// <see cref="System.Xml.Xsl.XsltException"/> / <see cref="XmlException"/> fırlatabilir.
/// </summary>
public interface IXsltHtmlRenderer
{
    XsltRenderResult Render(string xslt, string xmlContent);
}

public sealed class XsltHtmlRenderer : IXsltHtmlRenderer
{
    public XsltRenderResult Render(string xslt, string xmlContent)
    {
        var sw = Stopwatch.StartNew();
        var transform = SecureXslt.Compile(xslt);
        var compileMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var xmlDoc = new XmlDocument { XmlResolver = null };
        using (var xmlReader = XmlReader.Create(new StringReader(xmlContent), SecureXslt.ReaderSettings()))
            xmlDoc.Load(xmlReader);
        var parseMs = sw.ElapsedMilliseconds;

        sw.Restart();
        using var writer = new StringWriter();
        transform.Transform(xmlDoc, arguments: null, writer);
        var transformMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var html = writer.ToString();
        var serializeMs = sw.ElapsedMilliseconds;

        return new XsltRenderResult(html, parseMs, compileMs, transformMs, serializeMs);
    }
}
