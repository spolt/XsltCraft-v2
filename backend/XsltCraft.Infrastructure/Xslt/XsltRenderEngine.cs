using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Infrastructure.Xslt;

public class XsltRenderEngine : IRenderEngine
{
    private readonly ITemplateCache _cache;

    public XsltRenderEngine(ITemplateCache cache)
    {
        _cache = cache;
    }

    public string Render(string templateId, string xml)
    {
        var template = _cache.Get(templateId);

        using var xmlReader = XmlReader.Create(new StringReader(xml));

        using var writer = new StringWriter();

        template.Transform.Transform(xmlReader, null, writer);

        return writer.ToString();
    }
}