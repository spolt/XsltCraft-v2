using Microsoft.AspNetCore.Hosting;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Infrastructure.Templates
{
    public class TemplateDiscoveryEngine : ITemplateDiscovery
    {
        private readonly string _root;

        public TemplateDiscoveryEngine(IWebHostEnvironment env)
        {
            _root = Path.Combine(env.ContentRootPath, "templates");
        }

        public IEnumerable<string> Discover(string? root = null)
        {
            var path = root ?? _root;

            return Directory
                .EnumerateFiles(path, "template.xslt", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(x => x != null)!;
        }
    }
}