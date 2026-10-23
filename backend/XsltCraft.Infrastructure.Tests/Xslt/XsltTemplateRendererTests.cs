using System.Text;
using System.Xml;

using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Infrastructure.Xslt;

namespace XsltCraft.Infrastructure.Tests.Xslt;

/// <summary>
/// /api/render yolu (RenderController → IXsltTemplateRenderer.RenderAsync) — Saxon kaldırıldıktan
/// sonra .NET güvenli derleyici (IXsltHtmlRenderer / SecureXslt) üzerinden render edilir.
/// </summary>
public class XsltTemplateRendererTests
{
    private const string StoragePath = "templates/test.xslt";

    private const string InvoiceXml =
        "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" " +
        "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
        "<cbc:ID>ABC2026000000001</cbc:ID><cbc:PayableAmount>1234.5</cbc:PayableAmount></Invoice>";

    [Fact]
    public async Task RenderAsync_V2StylesheetWith1_0Functions_Renders()
    {
        const string xslt = """
            <xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
              <xsl:output method="html" encoding="UTF-8"/>
              <xsl:template match="/">
                <html><body>
                  <span id="no"><xsl:value-of select="concat('No: ', normalize-space(n1:Invoice/cbc:ID))"/></span>
                  <span id="amt"><xsl:value-of select="format-number(number(n1:Invoice/cbc:PayableAmount), '#,##0.00')"/></span>
                </body></html>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var (sut, id) = CreateSut(xslt);

        var html = await sut.RenderAsync(id.ToString(), LoadXml(InvoiceXml));

        Assert.Contains("No: ABC2026000000001", html);
        Assert.Contains("1,234.50", html);
    }

    [Fact]
    public async Task RenderAsync_ThreatRejected()
    {
        const string xslt = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <xsl:copy-of select="document('file:///C:/Windows/win.ini')"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        var (sut, id) = CreateSut(xslt);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RenderAsync(id.ToString(), LoadXml(InvoiceXml)));
        Assert.Contains("document()", ex.Message);
    }

    private static (XsltTemplateRenderer Sut, Guid Id) CreateSut(string xslt)
    {
        var id = Guid.NewGuid();
        var template = new Template { Id = id, Name = "test", XsltStoragePath = StoragePath };
        var sut = new XsltTemplateRenderer(
            new FakeTemplateRepository(template),
            new FakeStorageService(StoragePath, xslt),
            new XsltHtmlRenderer());
        return (sut, id);
    }

    private static XmlDocument LoadXml(string xml)
    {
        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(xml);
        return doc;
    }

    private sealed class FakeTemplateRepository(Template template) : ITemplateRepository
    {
        public IEnumerable<Template> GetAll() => [template];

        public Task<Template?> GetById(Guid id) => Task.FromResult(id == template.Id ? template : null);
    }

    private sealed class FakeStorageService(string path, string content) : IStorageService
    {
        public Task<Stream> ReadAsync(string relativePath) =>
            relativePath == path
                ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content)))
                : throw new FileNotFoundException(relativePath);

        public Task<string> WriteAsync(Stream content, string relativePath, string contentType) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string relativePath) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(relativePath == path);
    }
}
