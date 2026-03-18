using System.Xml;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly ITemplateRegistry _registry;
    private readonly IXsltTemplateRenderer _renderer;

    public TemplateService(
        ITemplateRegistry registry,
        IXsltTemplateRenderer renderer)
    {
        _registry = registry;
        _renderer = renderer;
    }

    public IEnumerable<TemplateDto> GetTemplates()
    {
        return _registry.GetAll()
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                Parameters = t.Metadata.Parameters,
                Templates = t.Metadata.Templates
            });
    }

    public Task<string> RenderAsync(string templateId, XmlDocument xml)
    {
        return _renderer.RenderAsync(templateId, xml);
    }

    public void Update(string id, string xslt)
    {
        // burada registry veya repository update yapılacak
    }
}