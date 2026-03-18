using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XsltCraft.Application.Interfaces
{
    public interface ITemplateRenderer
    {
        Task<string> RenderAsync(string templateId, XmlDocument xml);
    }
}
