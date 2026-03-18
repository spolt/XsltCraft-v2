using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Xslt;

public class TemplateCache : ITemplateCache
{
    private readonly ConcurrentDictionary<string, CompiledTemplate> _cache = new();
    private readonly XsltTemplateAnalyzer _analyzer;

    public TemplateCache(XsltTemplateAnalyzer analyzer)
    {
        _analyzer = analyzer;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var templatesPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "templates"
        );

        if (!Directory.Exists(templatesPath))
            return;

        var dirs = Directory.GetDirectories(templatesPath);

        foreach (var dir in dirs)
        {
            var xsltPath = Path.Combine(dir, "template.xslt");

            if (!File.Exists(xsltPath))
                continue;

            var id = Path.GetFileName(dir);

            _cache.TryAdd(id, CompileTemplate(id, xsltPath));
        }
    }

    private CompiledTemplate CompileTemplate(string id, string path)
    {
        var xslt = File.ReadAllText(path);

        var metadata = _analyzer.Analyze(xslt);

        var transform = new XslCompiledTransform();

        using var reader = XmlReader.Create(new StringReader(xslt));

        transform.Load(reader);

        return new CompiledTemplate
        {
            Id = id,
            Transform = transform,
            Metadata = metadata
        };
    }

    public CompiledTemplate Get(string id)
    {
        if (!_cache.TryGetValue(id, out var template))
            throw new Exception($"Template '{id}' not found");

        return template;
    }

    public IEnumerable<CompiledTemplate> GetAll()
    {
        return _cache.Values;
    }

    public void Reload(string id, string path)
    {
        var template = CompileTemplate(id, path);
        _cache[id] = template;
    }
}