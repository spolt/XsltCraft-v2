using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces;

public interface ITemplateDiscovery
{
    IEnumerable<string> Discover(string root);
}