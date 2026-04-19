using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;

namespace XsltCraft.Application.XPath;

public class XPathEvaluator : IXPathEvaluator
{
    public XPathEvaluateResponse Evaluate(string xpathExpression, string xmlContent)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // XPathDocument is optimised for XPath queries
            var xpathDoc = new XPathDocument(new StringReader(xmlContent));
            var nav = xpathDoc.CreateNavigator();

            // Use navigator's NameTable — guaranteed same atom references
            var nsMgr = new XmlNamespaceManager(nav.NameTable);

            // Collect namespaces via XPathNavigator namespace axis (same NameTable, always works)
            var nsNav = nav.Clone();
            nsNav.MoveToRoot();
            nsNav.MoveToFirstChild(); // move to root element
            if (nsNav.MoveToFirstNamespace(XPathNamespaceScope.All))
            {
                do
                {
                    var prefix = nsNav.Name;
                    var uri = nsNav.Value;
                    if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(uri))
                        nsMgr.AddNamespace(prefix, uri);
                } while (nsNav.MoveToNextNamespace(XPathNamespaceScope.All));
            }

            object rawResult;
            try
            {
                // Use the overload that takes IXmlNamespaceResolver directly
                rawResult = nav.Evaluate(xpathExpression, nsMgr);
            }
            catch (XPathException xex)
            {
                sw.Stop();
                return new XPathEvaluateResponse("error", [], sw.ElapsedMilliseconds, xex.Message);
            }

            sw.Stop();
            var items = new List<XPathResultItem>();

            if (rawResult is XPathNodeIterator iter)
            {
                while (iter.MoveNext())
                {
                    var cur = iter.Current!;
                    var kind = cur.NodeType switch
                    {
                        XPathNodeType.Element => "element",
                        XPathNodeType.Attribute => "attribute",
                        XPathNodeType.Text => "text",
                        XPathNodeType.Comment => "comment",
                        XPathNodeType.ProcessingInstruction => "pi",
                        _ => "node"
                    };
                    var name = string.IsNullOrEmpty(cur.Prefix)
                        ? cur.LocalName
                        : $"{cur.Prefix}:{cur.LocalName}";
                    items.Add(new XPathResultItem(kind, name, Truncate(cur.Value), null, null));
                }

                var resultKind = items.Count == 0 ? "empty" : "node-set";
                return new XPathEvaluateResponse(resultKind, items, sw.ElapsedMilliseconds, null);
            }
            else
            {
                // scalar: number, boolean, string
                items.Add(new XPathResultItem("atomic", "", Truncate(rawResult?.ToString() ?? ""), null, null));
                return new XPathEvaluateResponse("atomic", items, sw.ElapsedMilliseconds, null);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new XPathEvaluateResponse("error", [], sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length > 200 ? value[..200] + "…" : value;
    }
}
