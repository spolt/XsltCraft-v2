using XsltCraft.Application.XPath;

namespace XsltCraft.Tests.Eval;

/// <summary>
/// XPath konsolu (IXPathEvaluator) eval harness'i. Bilinen bir UBL-TR faturasına karşı
/// namespace-farkında değerlendirmenin doğru sonuç verdiğini ve geçersiz ifadenin
/// exception fırlatmadan "error" döndürdüğünü kilitler. İnsan-okur: docs/ecc/evaluations/xpath/.
///
/// Lokal: dotnet test --filter "Category=Eval"
/// </summary>
[Trait("Category", "Eval")]
public class XPathEvalTests
{
    private readonly XPathEvaluator _sut = new();

    // Minimal ama namespace-doğru UBL-TR fatura (n1/cbc/cac kökte tanımlı).
    private const string UblInvoice =
        """
        <n1:Invoice xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                    xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                    xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:ID>FAT2026000001</cbc:ID>
          <cbc:IssueDate>2026-06-20</cbc:IssueDate>
          <cac:AccountingSupplierParty>
            <cac:Party><cac:PartyName><cbc:Name>ACME Ltd</cbc:Name></cac:PartyName></cac:Party>
          </cac:AccountingSupplierParty>
          <cac:InvoiceLine><cbc:ID>1</cbc:ID></cac:InvoiceLine>
          <cac:InvoiceLine><cbc:ID>2</cbc:ID></cac:InvoiceLine>
          <cac:LegalMonetaryTotal><cbc:PayableAmount>1180.00</cbc:PayableAmount></cac:LegalMonetaryTotal>
        </n1:Invoice>
        """;

    [Fact]
    public void XPathCase001_InvoiceNumber_ResolvesSingleNode()
    {
        var r = _sut.Evaluate("/n1:Invoice/cbc:ID", UblInvoice);

        Assert.Null(r.Error);
        Assert.Equal("node-set", r.Kind);
        Assert.Single(r.Items);
        Assert.Equal("FAT2026000001", r.Items[0].Value);
        Assert.Equal("cbc:ID", r.Items[0].Name);
    }

    [Fact]
    public void XPathCase001_BoundBinding_ResolvesSupplierName()
    {
        var r = _sut.Evaluate("//cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name", UblInvoice);

        Assert.Null(r.Error);
        Assert.Equal("node-set", r.Kind);
        Assert.Equal("ACME Ltd", r.Items[0].Value);
    }

    [Fact]
    public void XPathCase001_CountFunction_ReturnsAtomic()
    {
        var r = _sut.Evaluate("count(//cac:InvoiceLine)", UblInvoice);

        Assert.Null(r.Error);
        Assert.Equal("atomic", r.Kind);
        Assert.Equal("2", r.Items[0].Value);
    }

    [Fact]
    public void XPathCase001_NoMatch_ReturnsEmpty()
    {
        var r = _sut.Evaluate("/n1:Invoice/cbc:DoesNotExist", UblInvoice);

        Assert.Null(r.Error);
        Assert.Equal("empty", r.Kind);
        Assert.Empty(r.Items);
    }

    [Fact]
    public void XPathCase001_InvalidExpression_ReturnsErrorNotThrow()
    {
        // Geçersiz XPath exception fırlatmamalı — kullanıcıya güvenli "error" sonucu dönmeli.
        var r = _sut.Evaluate("//cbc:[unclosed", UblInvoice);

        Assert.Equal("error", r.Kind);
        Assert.NotNull(r.Error);
    }
}
