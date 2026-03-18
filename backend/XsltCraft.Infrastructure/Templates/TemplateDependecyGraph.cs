using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Templates
{
    public class TemplateDependencyGraph
    {
        private readonly Dictionary<string, List<string>> _graph = new();

        public void Add(string template, IEnumerable<string> dependencies)
        {
            _graph[template] = dependencies.ToList();
        }

        public IEnumerable<string> GetDependents(string template)
        {
            return _graph
                .Where(x => x.Value.Contains(template))
                .Select(x => x.Key);
        }
    }
}
