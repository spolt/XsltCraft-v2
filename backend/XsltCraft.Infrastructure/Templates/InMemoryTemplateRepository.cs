using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Templates
{
    public class InMemoryTemplateRepository : ITemplateRepository
    {
        private readonly Dictionary<Guid, Template> _templates = new();

        public Task<Template?> GetById(Guid id)
        {
            _templates.TryGetValue(id, out var template);
            return Task.FromResult(template);
        }

        public IEnumerable<Template> GetAll()
        {
            return _templates.Values;
        }
    }
}
