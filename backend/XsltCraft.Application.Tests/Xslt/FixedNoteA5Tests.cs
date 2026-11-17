using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.Xslt;

/// <summary>
/// A5 (sayfalı, sayfa başına 2 kopya) şablonda sabit not: yalnız gerçek Notlar tablosunun başına, kopya başına
/// tek satır; seçici not okumaları ve değişkenlerdeki not döngüleri hedef alınmaz. Fixture anonim ve kırpılmıştır.
/// </summary>
public class FixedNoteA5Tests
{
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";
    private readonly FixedNoteInjector _sut = new();

    // Tek kopya: fatura bilgileri (seçici !#INTERNET# döngüsü + Satış Temsilcisi okuması — tuzak), kalemler,
    // son sayfada Notlar tablosu (Müşteri No döngüsü, genel not döngüsü, ödeme koşulu, IBAN) ve toplamlar.
    private const string Copy = """
              <xsl:for-each select="$XML">
                <table id="containerKck">
                  <tr><td>
                    <table id="invoiceDetailTblKck">
                      <tr><td>Fatura No</td><td><xsl:value-of select="n1:Invoice/cbc:ID"/></td></tr>
                      <xsl:if test="//n1:Invoice/cbc:Note[contains(.,'Satış Temsilcisi:')]">
                        <tr><td><xsl:value-of select="substring-after(//n1:Invoice/cbc:Note[contains(.,'Satış Temsilcisi:')],'Satış Temsilcisi:')"/></td></tr>
                      </xsl:if>
                      <xsl:for-each select="//n1:Invoice/cbc:Note[contains(.,'!#INTERNET#')]">
                        <tr><td>Web</td><td><xsl:value-of select="substring-before(substring-after(.,'!#INTERNET#|'),'|')"/></td></tr>
                      </xsl:for-each>
                      <tr><td><xsl:value-of select="n1:Invoice/cbc:UUID"/></td></tr>
                    </table>
                  </td></tr>
                  <tr><td>
                    <table id="lineTableKck">
                      <xsl:for-each select="n1:Invoice/cac:InvoiceLine"><tr><td><xsl:value-of select="cbc:ID"/></td></tr></xsl:for-each>
                    </table>
                  </td></tr>
                  <tr class="notesMainTrKck"><td id="notesKck">
                    <xsl:if test="$index = $count">
                      <table><tr>
                        <td>
                          <table id="notesTableKck">
                            <xsl:for-each select="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification">
                              <xsl:if test="./cbc:ID/@schemeID!='VKN' and ./cbc:ID/@schemeID!='TCKN'">
                                <tr><td><span class="noteLblKck">MÜŞTERİ NO: </span><xsl:value-of select="cbc:ID"/></td></tr>
                              </xsl:if>
                            </xsl:for-each>
                            <xsl:for-each select="//n1:Invoice/cbc:Note[not(contains(.,'!#')) and not(contains(.,'[Merkez'))
                                  and not(contains(.,'[RuhsatNo]')) and not(contains(.,'Satış Temsilcisi:'))]">
                              <tr>
                                <td class="capitalKck">
                                  <xsl:if test="not(starts-with(.,'Sevk')) and not(starts-with(.,'Müşteri'))">
                                    <xsl:value-of select="."/>
                                  </xsl:if>
                                </td>
                              </tr>
                            </xsl:for-each>
                            <xsl:if test="//n1:Invoice/cac:PaymentTerms/cbc:Note">
                              <tr><td>Ödeme Koşulu : <xsl:value-of select="//n1:Invoice/cac:PaymentTerms/cbc:Note"/></td></tr>
                            </xsl:if>
                            <tr><td><b>BANKA - IBAN:</b> TR00 0000 0000 0000 0000 0000 00</td></tr>
                          </table>
                        </td>
                        <td>
                          <table id="budgetContainerTableKck">
                            <tr><td><xsl:value-of select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount"/></td></tr>
                          </table>
                        </td>
                      </tr></table>
                    </xsl:if>
                  </td></tr>
                </table>
              </xsl:for-each>
        """;

    private static readonly string A5Xslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
          xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
          exclude-result-prefixes="n1 cbc cac">
          <xsl:output method="html" encoding="UTF-8"/>
          <xsl:variable name="XML" select="/"/>
          <xsl:variable name="linePerPage" select="10"/>
          <xsl:variable name="odemeTipi">
            <xsl:for-each select="//n1:Invoice/cbc:Note">
              <xsl:choose>
                <xsl:when test="substring(.,0,12) = 'Ödeme Tipi:'"><xsl:value-of select="."/></xsl:when>
              </xsl:choose>
            </xsl:for-each>
          </xsl:variable>
          <xsl:template match="/">
            <html><body>
              <xsl:variable name="total" select="count(n1:Invoice/cac:InvoiceLine)"/>
              <xsl:call-template name="Paging">
                <xsl:with-param name="count" select="ceiling($total div $linePerPage)"/>
              </xsl:call-template>
            </body></html>
          </xsl:template>
          <xsl:template name="Paging">
            <xsl:param name="index" select="1"/>
            <xsl:param name="count" select="1"/>
            <xsl:if test="$index &lt;= $count">
        {{COPY}}
        {{COPY}}
              <xsl:call-template name="Paging">
                <xsl:with-param name="index" select="$index + 1"/>
                <xsl:with-param name="count" select="$count"/>
              </xsl:call-template>
            </xsl:if>
          </xsl:template>
        </xsl:stylesheet>
        """.Replace("{{COPY}}", Copy);

    [Fact]
    public void Inserts_one_row_at_top_of_each_copys_notes_table_only()
    {
        var result = _sut.Inject(A5Xslt, "Sabit A5 notu", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        Assert.Equal(2, result.Targets);

        var doc = XDocument.Parse(result.Xslt);
        var injected = doc.Descendants(Xsl + "text").Where(t => t.Value == "Sabit A5 notu").ToList();
        Assert.Equal(2, injected.Count);

        foreach (var text in injected)
        {
            var row = text.Ancestors("tr").First();
            Assert.Equal("notesTableKck", (string?)row.Parent?.Attribute("id"));
            Assert.Same(row, row.Parent!.Elements().First()); // tablonun ilk satırı
            Assert.Equal("capitalKck", (string?)text.Parent?.Attribute("class")); // şablonun not stili
            Assert.DoesNotContain(text.Ancestors(Xsl + "for-each"), f => ((string?)f.Attribute("select"))!.Contains("Note"));
        }

        // Döngü dışı not filtresi (starts-with) snippet'e taşınmamalı; tuzaklar değişmemeli.
        Assert.Equal(CountOf(A5Xslt, "starts-with(.,'Sevk')"), CountOf(result.Xslt, "starts-with(.,'Sevk')"));
        Assert.Equal(Normalize(A5Xslt), Normalize(StripBlocks(result.Xslt)));
    }

    [Fact]
    public void Renders_note_once_per_copy()
    {
        var result = _sut.Inject(A5Xslt, "Sabit A5 notu", FixedNoteMode.Replace);
        Assert.Equal(FixedNoteStatus.Updated, result.Status);

        var transform = SecureXslt.Compile(result.Xslt);
        var invoice = new ThemeSampleXmlProvider().Get(DocumentType.Invoice);
        using var input = XmlReader.Create(new StringReader(invoice), SecureXslt.ReaderSettings());
        using var output = new StringWriter();
        transform.Transform(input, null, output);

        Assert.Equal(2, CountOf(output.ToString(), "Sabit A5 notu"));
    }

    [Fact]
    public void Replace_and_append_keep_one_block_per_copy()
    {
        var first = _sut.Inject(A5Xslt, "Birinci", FixedNoteMode.Replace);
        var second = _sut.Inject(first.Xslt, "İkinci", FixedNoteMode.Replace);
        Assert.Equal(2, CountOf(second.Xslt, "xc:fixed-note:start"));
        Assert.DoesNotContain("Birinci", second.Xslt);

        var third = _sut.Inject(second.Xslt, "Üçüncü", FixedNoteMode.Append);
        Assert.Equal(FixedNoteStatus.Updated, third.Status);
        Assert.Equal(2, CountOf(third.Xslt, "xc:fixed-note:start"));
        Assert.Equal(2, CountOf(third.Xslt, "İkinci"));
        Assert.Equal(2, CountOf(third.Xslt, "Üçüncü"));
    }

    // İki farklı yapıda, eşit puanlı not tablosu (ikisi de adlandırmasız, ikisi de genel döngü içeriyor).
    private const string AmbiguousXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <xsl:template match="/">
            <html><body>
              <table class="a"><xsl:for-each select="//n1:Invoice/cbc:Note"><tr><td><xsl:value-of select="."/></td></tr></xsl:for-each></table>
              <div><table class="b"><xsl:for-each select="//n1:Invoice/cbc:Note"><tr><th><xsl:value-of select="."/></th></tr></xsl:for-each></table></div>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    [Fact]
    public void Ambiguous_sections_ask_first_then_pick_first_on_confirmation()
    {
        var ask = _sut.Inject(AmbiguousXslt, "Not", FixedNoteMode.Replace);
        Assert.Equal(FixedNoteStatus.Ambiguous, ask.Status);
        Assert.Equal(AmbiguousXslt, ask.Xslt); // onaysız değişmez

        var pick = _sut.Inject(AmbiguousXslt, "Not", FixedNoteMode.Replace, FixedNoteAmbiguity.PickFirst);
        Assert.Equal(FixedNoteStatus.Updated, pick.Status);
        var text = XDocument.Parse(pick.Xslt).Descendants(Xsl + "text").Single(t => t.Value == "Not");
        Assert.Equal("a", (string?)text.Ancestors("table").First().Attribute("class"));
    }

    private static string StripBlocks(string xslt)
    {
        while (xslt.IndexOf("<!-- xc:fixed-note:start -->", StringComparison.Ordinal) is var s and >= 0)
        {
            var e = xslt.IndexOf("<!-- xc:fixed-note:end -->", s, StringComparison.Ordinal) + "<!-- xc:fixed-note:end -->".Length;
            xslt = xslt[..s] + xslt[e..];
        }
        return xslt;
    }

    private static string Normalize(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
