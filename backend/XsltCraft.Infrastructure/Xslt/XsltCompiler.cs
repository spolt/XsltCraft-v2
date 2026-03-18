using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Infrastructure.Xslt
{
    public class XsltCompiler
    {
        private readonly ConcurrentDictionary<string, XslCompiledTransform> _cache = new();

        public XslCompiledTransform GetOrCompile(string id, string xslt)
        {
            return _cache.GetOrAdd(id, _ =>
            {
                var transform = new XslCompiledTransform();

                using var reader = XmlReader.Create(new StringReader(xslt));

                transform.Load(reader);

                return transform;
            });
        }
    }
}
