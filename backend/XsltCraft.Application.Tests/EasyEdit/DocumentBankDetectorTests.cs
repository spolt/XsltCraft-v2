using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.EasyEdit;

/// <summary>Belgenin kendi banka/IBAN satırlarının tespiti (XSLT çalıştırılmaz).</summary>
public class DocumentBankDetectorTests
{
    internal const string Iban1 = "TR66 0006 7010 0000 0020 1934 37";
    internal const string Iban2 = "TR330006100519786457841326";

    internal static string Theme(string body) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
            xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" exclude-result-prefixes="n1 cbc cac">
          <xsl:output method="html"/>
          <xsl:template match="/">
            <html><body>
              <xsl:for-each select="/">
                <div id="header"><xsl:value-of select="//cbc:ID"/></div>
                {body}
              </xsl:for-each>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    internal static string TwoColumnTable(string extraRow = "") => Theme($"""
                <table id="banks">
                  <tr><th>Banka</th><th>IBAN</th></tr>
                  <tr><td>Ziraat Bankası</td><td>{Iban1}</td></tr>
                  <tr><td>Halkbank</td><td>{Iban2}</td></tr>{extraRow}
                </table>
        """);

    internal const string SampleName = "ÖRNEK BANKA";
    internal const string SampleIban = "TR00 0000 0000 0000 0000 0000 00";
    internal const string SampleStyle = "color: rgb(68, 68, 68); font-family: verdana, geneva, sans-serif; font-size: 10px;";

    /// <summary>Hücrelerde stilli <c>span</c> sarmalayıcı ve <c>&amp;#xa0;</c> dolgusu olan tek satırlı banka tablosu.</summary>
    internal static string SpanPaddedTable() => Theme($"""
                <table border="1" cellpadding="1" cellspacing="1" style="width: 800px;">
                  <tbody><tr>
                    <td style="width: 239px;">
                      <span style="{SampleStyle}">&#xa0; &#xa0;&#xa0;{SampleName}</span>
                    </td>
                    <td style="width: 450px;">
                      <span style="{SampleStyle}">&#xa0; &#xa0;&#xa0;IBAN:&#xa0;{SampleIban}</span>
                    </td>
                  </tr></tbody>
                </table>
        """);

    private static DocumentBankDetection Detect(string xslt) => DocumentBankDetector.Detect(xslt, DocumentType.Invoice);

    [Fact]
    public void SingleCell_NameFromPrefix()
    {
        var xslt = Theme($"<div>YAPIKREDİ | IBAN: {Iban1}</div>");

        var d = Detect(xslt);

        Assert.True(d.Found);
        var row = Assert.Single(d.Rows);
        Assert.Equal("YAPIKREDİ", row.BankName);
        Assert.Equal(Iban1, row.Iban);
        Assert.True(row.BankNameEditable);
        Assert.True(row.Deletable);
        Assert.Null(row.TableId);
        Assert.Equal(Iban1, xslt[row.IbanStart..row.IbanEnd]);
        Assert.Equal("YAPIKREDİ", xslt[row.NameStart!.Value..row.NameEnd!.Value]);
        Assert.True(d.CanAdd);
        Assert.Equal(row.RowEnd, d.InsertOffset);
        Assert.Equal("\n        ", d.InsertIndent!.Replace("\r", ""));
        Assert.Equal(xslt[..row.IbanStart].Count(c => c == '\n') + 1, row.Line);
    }

    [Fact]
    public void TwoColumns_NamesFromCells_TableRemovable()
    {
        var xslt = TwoColumnTable();

        var d = Detect(xslt);

        Assert.Equal(2, d.Rows.Count);
        Assert.Equal(["Ziraat Bankası", "Halkbank"], d.Rows.Select(r => r.BankName));
        Assert.Equal([Iban1, Iban2], d.Rows.Select(r => r.Iban));
        Assert.All(d.Rows, r => Assert.True(r.BankNameEditable && r.Deletable));
        Assert.All(d.Rows, r => Assert.Equal(0, r.TableId));
        var table = Assert.Single(d.Tables);
        Assert.True(table.Removable);
        Assert.Equal([0, 1], table.RowIndexes);
        Assert.StartsWith("<table", xslt[table.Start..]);
        Assert.True(d.CanAdd);
        Assert.Equal(d.Rows[1].RowEnd, d.InsertOffset);
    }

    [Fact]
    public void SpanWrappedPaddedCells_NameEditable_CanAdd()
    {
        var xslt = SpanPaddedTable();

        var d = Detect(xslt);

        var row = Assert.Single(d.Rows);
        Assert.Equal(SampleName, row.BankName);
        Assert.Equal(SampleIban, row.Iban);
        Assert.True(row.BankNameEditable);
        Assert.True(row.Deletable);
        Assert.Equal(SampleName, xslt[row.NameStart!.Value..row.NameEnd!.Value]);
        Assert.EndsWith("IBAN:&#xa0;", xslt[..row.IbanStart]);
        Assert.Equal(SampleIban, xslt[row.IbanStart..row.IbanEnd]);
        Assert.True(d.CanAdd);
        Assert.Equal(row.RowEnd, d.InsertOffset);
        Assert.True(Assert.Single(d.Tables).Removable);
    }

    [Theory]
    [InlineData("<b><span>&#160;Örnek Banka -</span></b>", "Örnek Banka")]
    [InlineData("<strong> &#xA0;Örnek Banka:&#xa0;</strong>", "Örnek Banka")]
    [InlineData("<font><i> Örnek <!-- x --> Banka</i></font>", "")]
    [InlineData("<span>Örnek</span><span>Banka</span>", "")]
    [InlineData("<a>Örnek Banka</a>", "")]
    public void NameCell_WrapperChain(string cell, string expected)
    {
        var d = Detect(Theme($"<table><tr><td>{cell}</td><td>{Iban1}</td></tr></table>"));

        var row = Assert.Single(d.Rows);
        Assert.Equal(expected, row.BankName);
        Assert.Equal(expected.Length > 0, row.BankNameEditable);
        Assert.Equal(expected.Length > 0, d.CanAdd);
    }

    [Fact]
    public void EntityInsideTrimmedName_NotEditable()
    {
        var d = Detect(Theme($"<table><tr><td><span>&#xa0;A&amp;B Bankası&#xa0;</span></td><td>{Iban1}</td></tr></table>"));

        var row = Assert.Single(d.Rows);
        Assert.Equal("A&B Bankası", row.BankName);
        Assert.False(row.BankNameEditable);
        Assert.False(d.CanAdd);
    }

    [Fact]
    public void PaddedPrefix_NameFromSingleCell()
    {
        var xslt = Theme($"<div>&#xa0;&#xa0;{SampleName} | IBAN:&#xa0;{Iban1}</div>");

        var row = Assert.Single(Detect(xslt).Rows);

        Assert.Equal(SampleName, row.BankName);
        Assert.True(row.BankNameEditable);
        Assert.Equal(SampleName, xslt[row.NameStart!.Value..row.NameEnd!.Value]);
    }

    [Fact]
    public void LoopValueOfAndFixedNote_NotDetected()
    {
        var xslt = Theme($"""
                <xsl:for-each select="//cac:PaymentMeans"><div>Banka IBAN: {Iban1}</div></xsl:for-each>
                <div>IBAN: <xsl:value-of select="'{Iban2}'"/></div>
                <div data-xc-fixed-note="before">Not IBAN: {Iban2}</div>
                <script>var x = '{Iban1}';</script>
        """);

        var d = Detect(xslt);

        Assert.False(d.Found);
        Assert.Empty(d.Rows);
        Assert.False(d.CanAdd);
    }

    [Fact]
    public void XslTextHolder_Detected()
    {
        var xslt = Theme($"""
                <table><tr><td>Vakıfbank</td><td><xsl:text>{Iban1}</xsl:text></td></tr></table>
                <p>Banka: <xsl:text>{Iban2}</xsl:text></p>
        """);

        var d = Detect(xslt);

        Assert.Equal(2, d.Rows.Count);
        Assert.Equal("Vakıfbank", d.Rows[0].BankName);
        Assert.True(d.Rows[0].BankNameEditable);
        Assert.Equal(Iban2, d.Rows[1].Iban);
        Assert.False(d.Rows[1].BankNameEditable); // xsl:text önekte ad yok
        Assert.False(d.CanAdd); // satırlar farklı ebeveynde
    }

    [Fact]
    public void DisableOutputEscaping_NotDetected()
    {
        var d = Detect(Theme($"""<p><xsl:text disable-output-escaping="yes">{Iban1}</xsl:text></p>"""));

        Assert.False(d.Found);
    }

    [Fact]
    public void TooManyCandidates_NotFound()
    {
        var body = string.Concat(Enumerable.Range(0, 4 * DocumentBankDetector.MaxRows + 1)
            .Select(i => $"<div>Banka {i} IBAN: {Iban1}</div>"));

        Assert.False(Detect(Theme(body)).Found);
    }

    [Fact]
    public void TemplateRowWithDisableOutputEscaping_CannotAdd()
    {
        var d = Detect(TwoColumnTable("""
            <tr><td>Akbank</td><td>TR120001000000000000000001</td><td><xsl:text disable-output-escaping="yes">-</xsl:text></td></tr>
            """));

        Assert.Equal(3, d.Rows.Count);
        Assert.False(d.CanAdd);
    }

    [Fact]
    public void TemplateRowWithBraceInAttribute_CannotAdd()
    {
        var d = Detect(TwoColumnTable("""
            <tr><td title="{//cbc:ID}">Akbank</td><td>TR120001000000000000000001</td></tr>
            """));

        Assert.Equal(3, d.Rows.Count);
        Assert.False(d.CanAdd);
    }

    [Fact]
    public void EntityInName_NotEditable()
    {
        var d = Detect(Theme($"<div>A&amp;B Bankası IBAN: {Iban1}</div>"));

        var row = Assert.Single(d.Rows);
        Assert.Equal("A&B Bankası", row.BankName);
        Assert.False(row.BankNameEditable);
        Assert.False(d.CanAdd);
    }

    [Fact]
    public void XcSlotInRow_NotDeletable_CannotAdd()
    {
        var d = Detect(TwoColumnTable("""
            <tr><td>Akbank</td><td>TR120001000000000000000001</td><td><div data-xc-slot="bank-box" data-xc-inserted="1"></div></td></tr>
            """));

        Assert.Equal(3, d.Rows.Count);
        Assert.False(d.Rows[2].Deletable);
        Assert.True(d.Rows[0].Deletable);
        Assert.False(d.CanAdd);
        Assert.Null(d.InsertOffset);
        Assert.False(Assert.Single(d.Tables).Removable);
    }

    [Fact]
    public void TableWithXslRow_NotRemovable()
    {
        var d = Detect(TwoColumnTable("""
            <tr><td>Şube</td><td><xsl:value-of select="//cbc:ID"/></td></tr>
            """));

        Assert.Equal(2, d.Rows.Count);
        Assert.False(Assert.Single(d.Tables).Removable);
        Assert.True(d.CanAdd);
    }

    [Fact]
    public void TwoIbansInSameRow_RowSkipped()
    {
        var d = Detect(Theme($"""
                <table><tr><td>{Iban1}</td><td>{Iban2}</td></tr></table>
                <div>Halkbank IBAN: {Iban2}</div>
        """));

        var row = Assert.Single(d.Rows);
        Assert.Equal("Halkbank", row.BankName);
    }

    [Fact]
    public void InvalidInput_NotFound()
    {
        Assert.False(Detect("<not-xml").Found);
        Assert.False(Detect("").Found);
        Assert.False(Detect($"<root>{Iban1}</root>").Found);
    }

    [Fact]
    public void ToInfo_ProjectsRows()
    {
        var info = Detect(TwoColumnTable()).ToInfo();

        Assert.True(info.Found);
        Assert.True(info.CanAdd);
        Assert.Equal(["Ziraat Bankası", "Halkbank"], info.Rows.Select(r => r.BankName));
        Assert.Equal([0, 1], info.Rows.Select(r => r.Index));
    }

    [Fact]
    public void TrIbanExact()
    {
        Assert.Matches(TrIban.Exact(), Iban1);
        Assert.Matches(TrIban.Exact(), Iban2);
        Assert.DoesNotMatch(TrIban.Exact(), Iban2 + "1");
        Assert.DoesNotMatch(TrIban.Exact(), "TR33 0006 1005 1978 6457 8413 2");
        Assert.DoesNotMatch(TrIban.Exact(), "TR33  0006100519786457841326");
    }
}
