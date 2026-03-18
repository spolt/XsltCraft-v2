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
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        return await _renderer.RenderPreviewAsync(xslt, xmlDoc);
    }
}