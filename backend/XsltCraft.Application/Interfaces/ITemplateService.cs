using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using XsltCraft.Application.DTO;

namespace XsltCraft.Application.Interfaces
{
    public interface ITemplateService
    {
        IEnumerable<TemplateDto> GetTemplates();

        Task<string> RenderAsync(string templateId, XmlDocument xml);

        void Update(string id, string xslt);
    }
}
