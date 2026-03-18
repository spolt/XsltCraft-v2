
using System.Xml.Xsl;

namespace XsltCraft.Domain.Entities
{
    public class CompiledTemplate
    {
        public string Id { get; set; }

        public XslCompiledTransform Transform { get; set; }

        public TemplateMetadata Metadata { get; set; }
    }
}
