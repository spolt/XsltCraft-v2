using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using Saxon.Api;

namespace XsltCraft.Application.XPath;

public class XPathEvaluator : IXPathEvaluator
{
    private readonly Processor _processor;

    public XPathEvaluator()
    {
        _processor = new Processor();
        try { _processor.SetProperty("http://saxon.sf.net/feature/lineNumbering", "true"); }
        catch { /* best-effort */ }
    }

    public XPathEvaluateResponse Evaluate(string xpathExpression, string xmlContent)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Parse XML with Saxon (line numbering best-effort)
            var builder = _processor.NewDocumentBuilder();
            builder.BaseUri = new Uri("file:///");
            XdmNode doc;
            using (var sr = new StringReader(xmlContent))
            {
                doc = builder.Build(sr);
            }

            // Declare namespaces from root element
            var compiler = _processor.NewXPathCompiler();
            foreach (var (prefix, uri) in ExtractNamespaces(xmlContent))
            {
                try { compiler.DeclareNamespace(prefix, uri); }
                catch { }
            }

            var executable = compiler.Compile(xpathExpression);
            var selector  = executable.Load();
            selector.ContextItem = doc;
            var result = selector.Evaluate();
            sw.Stop();

            var items = new List<XPathResultItem>();
            foreach (var item in result)
            {
                if (item is XdmNode node)
                {
                    var kind = node.NodeKind switch
                    {
                        XmlNodeType.Element               => "element",
                        XmlNodeType.Attribute             => "attribute",
                        XmlNodeType.Text                  => "text",
                        XmlNodeType.Comment               => "comment",
                        XmlNodeType.ProcessingInstruction => "pi",
                        _                                 => "node"
                    };

                    var qname  = node.NodeName;
                    var name   = qname is null ? "" :
                        string.IsNullOrEmpty(qname.Prefix)
                            ? qname.LocalName
                            : $"{qname.Prefix}:{qname.LocalName}";

                    var (line, col) = TryGetLineInfo(node);

                    items.Add(new XPathResultItem(
                        kind, name, Truncate(node.StringValue), line, col));
                }
                else if (item is XdmAtomicValue atomic)
                {
                    items.Add(new XPathResultItem(
                        "atomic", "", Truncate(atomic.ToString() ?? ""), null, null));
                }
            }

            var kind2 = items.Count == 0        ? "empty"
                      : items[0].Kind == "atomic" ? "atomic"
                      :                             "node-set";

            return new XPathEvaluateResponse(kind2, items, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new XPathEvaluateResponse("error", [], sw.ElapsedMilliseconds, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (int? Line, int? Column) TryGetLineInfo(XdmNode node)
    {
        try
        {
            var impl = node.Implementation;
            if (impl is null) return (null, null);
            var t = impl.GetType();
            var lm = t.GetMethod("getLineNumber");
            var cm = t.GetMethod("getColumnNumber");
            if (lm is null) return (null, null);
            var line = (int)(lm.Invoke(impl, null) ?? 0);
            var col  = cm is null ? 0 : (int)(cm.Invoke(impl, null) ?? 0);
            return line > 0 ? (line, col > 0 ? col : null) : (null, null);
        }
        catch { return (null, null); }
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length > 200 ? value[..200] + "…" : value;
    }

    private static Dictionary<string, string> ExtractNamespaces(string xmlContent)
    {
        var ns = new Dictionary<string, string>();
        try
        {
            var root = XDocument.Parse(xmlContent, LoadOptions.None).Root;
            if (root is null) return ns;
            foreach (var a in root.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                var prefix = a.Name.LocalName == "xmlns" ? "" : a.Name.LocalName;
                if (!string.IsNullOrEmpty(prefix))
                    ns.TryAdd(prefix, a.Value);
            }
        }
        catch { }
        return ns;
    }
}
