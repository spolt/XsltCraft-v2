using Microsoft.AspNetCore.Hosting;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Repositories;

/// <summary>
/// Faz 1 geliştirme ortamı için dosya sistemi tabanlı geçici uygulama.
/// Faz 2'de DB tabanlı repository ile değiştirilecektir.
/// </summary>
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

        if (!Directory.Exists(root))
            return [];

        return Directory.GetDirectories(root)
            .Where(d => File.Exists(Path.Combine(d, "template.xslt")))
            .Select(LoadTemplate);
    }

    public Task<Template?> GetById(Guid id)
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");
        var dir = Path.Combine(root, id.ToString());

        if (!Directory.Exists(dir))
            return Task.FromResult<Template?>(null);

        return Task.FromResult<Template?>(LoadTemplate(dir));
    }

    private static Template LoadTemplate(string dir)
    {
        var xsltPath = Path.Combine(dir, "template.xslt");
        var folderName = Path.GetFileName(dir);

        return new Template
        {
            Id = Guid.TryParse(folderName, out var id) ? id : Guid.NewGuid(),
            Name = folderName,
            XsltStoragePath = xsltPath,
            IsFreeTheme = true
        };
    }
}
