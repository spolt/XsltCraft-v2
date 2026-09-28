using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Infrastructure.Xslt
{
    public class XsltCompiler
    {
        private readonly ConcurrentDictionary<string, XslCompiledTransform> _cache = new();

        public XslCompiledTransform GetOrCompile(string id, string xslt)
        {
            return _cache.GetOrAdd(id, _ => SecureXslt.Compile(xslt));
        }
    }
}
