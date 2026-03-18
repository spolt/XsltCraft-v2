using System.Xml.Linq;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Xslt;

public class XsltTemplateAnalyzer
{
    public TemplateMetadata Analyze(string xslt)
    {
        var metadata = new TemplateMetadata();

        var doc = XDocument.Parse(xslt);

        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";

        // PARAMETERS
        metadata.Parameters = doc
            .Descendants(xsl + "param")
            .Select(p => p.Attribute("name")?.Value)
            .Where(x => x != null)
            .ToList();

        // TEMPLATES
        metadata.Templates = doc
            .Descendants(xsl + "template")
            .Select(t =>
                t.Attribute("name")?.Value ??
                t.Attribute("match")?.Value
            )
            .Where(x => x != null)
            .ToList();

        // CALL TEMPLATES
        metadata.CallTemplates = doc
            .Descendants(xsl + "call-template")
            .Select(t => t.Attribute("name")?.Value)
            .Where(x => x != null)
            .ToList();

        // XPATH EXPRESSIONS
        var xpathAttributes = new[] { "select", "test" };

        metadata.XPaths = doc
            .Descendants()
            .Attributes()
            .Where(a => xpathAttributes.Contains(a.Name.LocalName))
            .Select(a => a.Value)
            .Distinct()
            .ToList();

        return metadata;
    }
}