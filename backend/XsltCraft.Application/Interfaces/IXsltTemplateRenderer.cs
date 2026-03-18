using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XsltCraft.Application.Interfaces
{
    public interface IXsltTemplateRenderer
    {
        Task<string> RenderAsync(string templateId, XmlDocument xml);

        Task<string> RenderPreviewAsync(string xslt, XmlDocument xml);
    }
}
