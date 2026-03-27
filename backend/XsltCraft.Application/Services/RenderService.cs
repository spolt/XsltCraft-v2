using System.IO;
using System.Xml;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Application.Services;

public class RenderService
{
    private readonly ITemplateService _templateService;
    private readonly IXsltTemplateRenderer _renderer;

    public RenderService(
        ITemplateService templateService,
        IXsltTemplateRenderer renderer)
    {
        _templateService = templateService;
        _renderer = renderer;
    }

    public Task<string> RenderAsync(string templateId, XmlDocument xml)
    {
        return _templateService.RenderAsync(templateId, xml);
    }

    public async Task<string> RenderPreviewAsync(string xml, string xslt)
    {
        var xmlReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var xmlDoc = new XmlDocument { XmlResolver = null };
        using var xmlReader = XmlReader.Create(new StringReader(xml), xmlReaderSettings);
        xmlDoc.Load(xmlReader);

        return await _renderer.RenderPreviewAsync(xslt, xmlDoc);
    }
}