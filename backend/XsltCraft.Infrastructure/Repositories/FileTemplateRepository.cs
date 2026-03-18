using Microsoft.AspNetCore.Hosting;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Repositories;

public class FileTemplateRepository : ITemplateRepository
{
    private readonly IWebHostEnvironment _env;

    public FileTemplateRepository(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IEnumerable<Template> GetAll()
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");

        var dirs = Directory.GetDirectories(root)
        .Where(d => File.Exists(Path.Combine(d, "template.xslt")));

        return dirs.Select(LoadTemplate);
    }

    public Task<Template?> GetById(string id)
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");
        var dir = Path.Combine(root, id);

        if (!Directory.Exists(dir))
            return Task.FromResult<Template?>(null);

        var template = LoadTemplate(dir);

        return Task.FromResult<Template?>(template);
    }

    private Template LoadTemplate(string dir)
    {
        var xsltPath = Path.Combine(dir, "template.xslt");

        var xslt = File.ReadAllText(xsltPath);

        return new Template
        {
            Id = Path.GetFileName(dir),
            Name = Path.GetFileName(dir),
            XsltContent = xslt
        };
    }
}