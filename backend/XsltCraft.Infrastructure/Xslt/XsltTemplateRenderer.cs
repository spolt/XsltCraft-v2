using Saxon.Api;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Interfaces;
using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.Infrastructure.Xslt;

public class XsltTemplateRenderer : IXsltTemplateRenderer
{
    private readonly ITemplateRepository _repository;
    private readonly IStorageService _storage;

    public XsltTemplateRenderer(ITemplateRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<string> RenderPreviewAsync(string xslt, XmlDocument xml)
    {
        var transform = new XslCompiledTransform();

        using var xsltReader = XmlReader.Create(new StringReader(xslt));
        transform.Load(xsltReader);

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

        var processor = new Processor();
        var compiler = processor.NewXsltCompiler();

        using var xsltReader = new StringReader(xslt);
        var executable = compiler.Compile(xsltReader);
        var transformer = executable.Load();

        using var xmlReader = XmlReader.Create(new StringReader(xml.OuterXml));
        var inputNode = processor.NewDocumentBuilder().Build(xmlReader);
        transformer.InitialContextNode = inputNode;

        using var sw = new StringWriter();
        var serializer = processor.NewSerializer(sw);
        transformer.Run(serializer);

        return sw.ToString();
    }
}
