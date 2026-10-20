using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>Temizlenmiş GİB şablonu fixture'ları (gömülü görseller 1×1 PNG) ve ortak servis kurulumu.</summary>
internal static class ThemeFixtures
{
    public const string TestTheme = "test-theme.xslt";
    public const string GibEInvoice = "gib-einvoice.xslt";
    public const string GibTemplate = "gib-template.xslt";
    public const string GibGeneral = "gib-general.xslt";
    public const string GibEIrsaliye = "gib-eirsaliye.xslt";

    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    /// <summary>
    /// Firma temalarında sık görülen yapının sentetik karşılığı (müşteri verisi yok): başlıkta satıcı + GİB logosu +
    /// karekodun yanında statik firma logosu (float, 175×75), alıcı satırında kaşe görseli (200×150; <c>alt</c> yine
    /// "company_logo" — öneri konuma dayanmalı), belge bilgileri, ETTN, kalemler, toplamlar, notlar.
    /// </summary>
    public static readonly string CompanyImagesTheme = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
            exclude-result-prefixes="n1 cac cbc">
          <xsl:output method="html" indent="no" encoding="UTF-8"/>
          <xsl:variable name="XML" select="/"/>
          <xsl:template match="/">
            <html>
              <head><title>e-Belge</title></head>
              <body style="width:800px;">
                <xsl:for-each select="$XML">
                  <!-- Başlık: satıcı - GİB logosu - karekod + firma logosu -->
                  <table style="width:100%;"><tbody><tr>
                    <td style="width:40%;">
                      <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                        <xsl:value-of select="cac:PartyName/cbc:Name"/><br/>
                        <xsl:text>VKN: </xsl:text><xsl:value-of select="cac:PartyIdentification/cbc:ID"/>
                      </xsl:for-each>
                    </td>
                    <td style="width:20%; text-align:center;">
                      <img style="width:91px;" alt="E-Fatura Logo" src="__PNG__"/>
                      <h1>e-FATURA</h1>
                    </td>
                    <td style="width:40%; text-align:center;">
                      <div id="qrcode" style="float:right;"/>
                      <img style="float:right; width: 175px; height: 75px" src="__PNG__" alt="company_logo"/>
                      <br/>
                      <img id="code128" style="width: 200px; height: 70px; float:right;"/>
                    </td>
                  </tr></tbody></table>
                  <!-- Alıcı - kaşe - belge bilgileri -->
                  <table><tbody><tr>
                    <td style="width:30%;">
                      <table id="customerPartyTable"><tbody><tr><td>
                        <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                          <xsl:value-of select="cac:PartyName/cbc:Name"/>
                        </xsl:for-each>
                      </td></tr></tbody></table>
                    </td>
                    <td style="width:30%; text-align:center;">
                      <img src="__PNG__" alt="company_logo" width="200" height="150"/>
                    </td>
                    <td style="width:30%;">
                      <table><tbody>
                        <tr><td>Özelleştirme No:</td><td><xsl:value-of select="n1:Invoice/cbc:CustomizationID"/></td></tr>
                        <tr><td>Senaryo:</td><td><xsl:value-of select="n1:Invoice/cbc:ProfileID"/></td></tr>
                        <tr><td>Fatura Tarihi:</td><td><xsl:value-of select="n1:Invoice/cbc:IssueDate"/></td></tr>
                      </tbody></table>
                    </td>
                  </tr></tbody></table>
                  <table><tr><td>ETTN: <xsl:value-of select="n1:Invoice/cbc:UUID"/></td></tr></table>
                  <table id="lineTable"><tbody>
                    <xsl:for-each select="n1:Invoice/cac:InvoiceLine"><tr><td><xsl:value-of select="cbc:ID"/></td></tr></xsl:for-each>
                  </tbody></table>
                </xsl:for-each>
                <table id="budgetContainerTable"><tbody><tr><td>
                  <table><tbody><tr>
                    <td>Ödenecek Tutar</td>
                    <td><xsl:value-of select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount"/></td>
                  </tr></tbody></table>
                </td></tr></tbody></table>
                <table id="notesTable"><tbody><tr><td>
                  <xsl:for-each select="//n1:Invoice/cbc:Note"><b>Not: </b><xsl:value-of select="."/><br/></xsl:for-each>
                </td></tr></tbody></table>
                <div id="qrvalue" style="display:none"><xsl:value-of select="//n1:Invoice/cbc:UUID"/></div>
              </body>
            </html>
          </xsl:template>
        </xsl:stylesheet>
        """.Replace("__PNG__", Png);

    public static readonly IReadOnlyList<(string Name, DocumentType Type)> All =
    [
        (TestTheme, DocumentType.Invoice),
        (GibEInvoice, DocumentType.Invoice),
        (GibTemplate, DocumentType.Invoice),
        (GibGeneral, DocumentType.Invoice),
        (GibEIrsaliye, DocumentType.Despatch),
    ];

    public static string Load(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ThemeSlots", "Fixtures", name));

    public static readonly IXsltHtmlRenderer Renderer = new XsltHtmlRenderer();
    public static readonly IThemeSampleXmlProvider Samples = new ThemeSampleXmlProvider();
    public static IThemeSlotAnalyzer Analyzer() => new ThemeSlotAnalyzer(Renderer, Samples);
}
