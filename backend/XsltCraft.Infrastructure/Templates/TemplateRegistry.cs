using System.Text;
using System.Xml.Xsl;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Xslt;

namespace XsltCraft.Infrastructure.Templates
{
    public class TemplateRegistry : ITemplateRegistry
    {
        private readonly ITemplateCache _cache;

        public TemplateRegistry(ITemplateCache cache)
        {
            _cache = cache;
        }

        public CompiledTemplate Get(string id)
        {
            return _cache.Get(id);
        }

        public IEnumerable<CompiledTemplate> GetAll()
        {
            return _cache.GetAll();
        }

        public XslCompiledTransform GetTransform(string id)
        {
            var template = _cache.Get(id);
            return template.Transform;
        }
    }
}
