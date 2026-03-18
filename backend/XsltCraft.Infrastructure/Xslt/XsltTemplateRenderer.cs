using Saxon.Api;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Infrastructure.Xslt
{
    public class XsltTemplateRenderer : IXsltTemplateRenderer
    {
        private readonly ITemplateRepository _repository;

        public XsltTemplateRenderer(ITemplateRepository repository)
        {
            _repository = repository;
        }

        public async Task<string> RenderPreviewAsync(string xslt, XmlDocument xml)
        {
            var transform = new XslCompiledTransform();

            using var xsltReader = XmlReader.Create(new StringReader(xslt));
            transform.Load(xsltReader);

            using var sw = new StringWriter();

            transform.Transform(xml, null, sw);

            return await Task.FromResult(sw.ToString());
        }

        public async Task<string> RenderAsync(string templateId, XmlDocument xml)
        {
            var template = await _repository.GetById(templateId);

            if (template == null)
                throw new Exception($"Template '{templateId}' not found");

            var xslt = template.XsltContent;

            var processor = new Processor();
            var compiler = processor.NewXsltCompiler();

            using var xsltReader = new StringReader(xslt);
            var executable = compiler.Compile(xsltReader);

            var transformer = executable.Load();

            using var xmlReader = XmlReader.Create(new StringReader(xml.OuterXml));
            var inputNode = processor.NewDocumentBuilder().Build(xmlReader);

            transformer.InitialContextNode = inputNode;

            using var sw = new StringWriter();
            var serializer = processor.NewSerializer(sw);

            transformer.Run(serializer);

            return sw.ToString();
        }
    }
}