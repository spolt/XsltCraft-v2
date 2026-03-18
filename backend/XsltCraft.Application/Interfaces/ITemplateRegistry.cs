using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Xsl;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces
{
    public interface ITemplateRegistry
    {
        CompiledTemplate Get(string id);

        IEnumerable<CompiledTemplate> GetAll();

        XslCompiledTransform GetTransform(string id);
    }
}
