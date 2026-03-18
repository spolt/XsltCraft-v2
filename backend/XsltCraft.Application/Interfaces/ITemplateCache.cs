using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces
{
    public interface ITemplateCache
    {
        CompiledTemplate Get(string id);
        IEnumerable<CompiledTemplate> GetAll();
        void Reload(string id, string path);
    }
}
