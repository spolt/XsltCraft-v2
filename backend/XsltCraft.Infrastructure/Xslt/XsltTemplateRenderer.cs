using System.Xml;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Infrastructure.Xslt;

public class XsltTemplateRenderer : IXsltTemplateRenderer
{
    private readonly ITemplateRepository _repository;
    private readonly IStorageService _storage;
    private readonly IXsltHtmlRenderer _htmlRenderer;

    public XsltTemplateRenderer(ITemplateRepository repository, IStorageService storage, IXsltHtmlRenderer htmlRenderer)
    {
        _repository = repository;
        _storage = storage;
        _htmlRenderer = htmlRenderer;
    }

    public async Task<string> RenderPreviewAsync(string xslt, XmlDocument xml)
    {
        var transform = SecureXslt.Compile(xslt);

        using var sw = new StringWriter();
        transform.Transform(xml, null, sw);

        return await Task.FromResult(sw.ToString());
    }

    public async Task<string> RenderAsync(string templateId, XmlDocument xml)
    {
        if (!Guid.TryParse(templateId, out var id))
            throw new ArgumentException($"Geçersiz template ID: {templateId}");

        var template = await _repository.GetById(id)
            ?? throw new Exception($"Template '{templateId}' bulunamadı.");

        if (string.IsNullOrEmpty(template.XsltStoragePath))
            throw new InvalidOperationException($"Template '{templateId}' için XSLT storage yolu tanımlı değil.");

        await using var stream = await _storage.ReadAsync(template.XsltStoragePath);
        using var reader = new StreamReader(stream);
        var xslt = await reader.ReadToEndAsync();

        // Güvenlik: kayıtlı içerik de motora ulaşmadan fail-closed taranır
        // (document()/doc()/unparsed-text()/collection(), xsl:import/include).
        if (XsltSafety.FindThreat(xslt) is { } threat)
            throw new InvalidOperationException($"Template '{templateId}' güvenli değil: {threat}");

        // Derleme SecureXslt üzerinden (script/document() kapalı, resolver null); XML XXE korumalı okunur.
        return _htmlRenderer.Render(xslt, xml.OuterXml).Html;
    }
}
