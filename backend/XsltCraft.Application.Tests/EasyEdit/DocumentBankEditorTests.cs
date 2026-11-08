using System.Xml.Linq;

using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.Validation;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.Tests.EasyEdit.DocumentBankDetectorTests;

namespace XsltCraft.Application.Tests.EasyEdit;

/// <summary>Belgenin kendi banka satırlarının metin üzerinde düzenlenmesi.</summary>
public class DocumentBankEditorTests
{
    private const string Iban3 = "TR120001000000000000000001";
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    private static DocumentBankEditResult Apply(string xslt, params DocumentBankEditInput[] edits) =>
        DocumentBankEditor.Apply(xslt, DocumentType.Invoice, edits);

    private static DocumentBankDetection Detect(string xslt) => DocumentBankDetector.Detect(xslt, DocumentType.Invoice);

    private static DocumentBankEditInput Edit(int? index, string name, string iban) => new(index, name, iban, false);

    private static DocumentBankEditInput Remove(int index) => new(index, "", "", true);

    /// <summary>XSL (xsl:text hariç) elemanları ve görseller sırasıyla.</summary>
    private static List<string> Structure(string xslt) => XDocument.Parse(xslt).Descendants()
        .Where(e => (e.Name.Namespace == Xsl && e.Name.LocalName != "text") || e.Name.LocalName == "img")
        .Select(e => e.Name.LocalName + "|" + (string?)e.Attribute("select") + (string?)e.Attribute("src"))
        .ToList();

    private static void AssertValid(string original, DocumentBankEditResult result)
    {
        Assert.True(result.Changed);
        Assert.Null(XsltSafety.FindThreat(result.Xslt));
        Assert.Equal(Structure(original), Structure(result.Xslt));
    }

    [Fact]
    public void EmptyList_ReturnsSameReference()
    {
        var xslt = TwoColumnTable();

        Assert.Same(xslt, DocumentBankEditor.Apply(xslt, DocumentType.Invoice, null).Xslt);
        var result = Apply(xslt);
        Assert.Same(xslt, result.Xslt);
        Assert.False(result.Changed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Edit_ReplacesIbanAndName()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Edit(1, "Vakıf Katılım", Iban3));

        AssertValid(xslt, result);
        Assert.Empty(result.Warnings);
        var rows = Detect(result.Xslt).Rows;
        Assert.Equal(["Ziraat Bankası", "Vakıf Katılım"], rows.Select(r => r.BankName));
        Assert.Equal([Iban1, Iban3], rows.Select(r => r.Iban));
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(result.Xslt) - System.Text.Encoding.UTF8.GetByteCount(xslt), result.AddedBytes);
    }

    [Fact]
    public void UnchangedEdit_NotWritten()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Edit(0, "Ziraat Bankası", Iban1));

        Assert.False(result.Changed);
        Assert.Same(xslt, result.Xslt);
    }

    [Fact]
    public void Add_CopiesTemplateRow()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Edit(null, "Akbank", Iban3), Edit(null, "Garanti", Iban1));

        AssertValid(xslt, result);
        var d = Detect(result.Xslt);
        Assert.Equal(["Ziraat Bankası", "Halkbank", "Akbank", "Garanti"], d.Rows.Select(r => r.BankName));
        Assert.Equal([Iban1, Iban2, Iban3, Iban1], d.Rows.Select(r => r.Iban));
        Assert.Contains($"<tr><td>Akbank</td><td>{Iban3}</td></tr>", result.Xslt);
        Assert.True(Assert.Single(d.Tables).Removable);
        Assert.True(result.AddedBytes > 0);
    }

    [Fact]
    public void SpanPaddedTable_EditName_KeepsPaddingAndStyle()
    {
        var xslt = SpanPaddedTable();

        var result = Apply(xslt, Edit(0, "YENİ BANKA", Iban3));

        AssertValid(xslt, result);
        Assert.Contains($"<span style=\"{SampleStyle}\">&#xa0; &#xa0;&#xa0;YENİ BANKA</span>", result.Xslt);
        Assert.Contains($"<span style=\"{SampleStyle}\">&#xa0; &#xa0;&#xa0;IBAN:&#xa0;{Iban3}</span>", result.Xslt);
        var row = Assert.Single(Detect(result.Xslt).Rows);
        Assert.Equal("YENİ BANKA", row.BankName);
        Assert.Equal(Iban3, row.Iban);
    }

    [Fact]
    public void SpanPaddedTable_Add_CopiesTemplateRow()
    {
        var xslt = SpanPaddedTable();

        var result = Apply(xslt, Edit(null, "İKİNCİ BANKA", Iban3));

        AssertValid(xslt, result);
        var d = Detect(result.Xslt);
        Assert.Equal([SampleName, "İKİNCİ BANKA"], d.Rows.Select(r => r.BankName));
        Assert.Equal([SampleIban, Iban3], d.Rows.Select(r => r.Iban));
        Assert.All(d.Rows, r => Assert.True(r.BankNameEditable && r.Deletable));
        Assert.Contains($"<span style=\"{SampleStyle}\">&#xa0; &#xa0;&#xa0;İKİNCİ BANKA</span>", result.Xslt);
        Assert.Contains($"<span style=\"{SampleStyle}\">&#xa0; &#xa0;&#xa0;IBAN:&#xa0;{Iban3}</span>", result.Xslt);
        Assert.Equal(2, result.Xslt.Split("<td style=\"width: 239px;\">").Length - 1);
        Assert.True(d.CanAdd);
    }

    [Fact]
    public void SpanPaddedTable_Remove()
    {
        var xslt = SpanPaddedTable();
        var added = Apply(xslt, Edit(null, "İKİNCİ BANKA", Iban3)).Xslt;

        var result = Apply(added, Remove(0));

        AssertValid(added, result);
        var row = Assert.Single(Detect(result.Xslt).Rows);
        Assert.Equal("İKİNCİ BANKA", row.BankName);
        Assert.DoesNotContain(SampleIban, result.Xslt);

        var all = Apply(xslt, Remove(0));
        AssertValid(xslt, all);
        Assert.DoesNotContain("<table", all.Xslt);
        Assert.False(Detect(all.Xslt).Found);
    }

    [Fact]
    public void Remove_DeletesRow()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Remove(0));

        AssertValid(xslt, result);
        var row = Assert.Single(Detect(result.Xslt).Rows);
        Assert.Equal("Halkbank", row.BankName);
        Assert.Equal(0, result.AddedBytes);
    }

    [Fact]
    public void Escapes_SpecialCharacters()
    {
        var xslt = Theme($"<div>Ziraat IBAN: {Iban1}</div>");

        var result = Apply(xslt, Edit(0, "A<&>B", Iban1));

        AssertValid(xslt, result);
        Assert.Contains("A&lt;&amp;&gt;B IBAN:", result.Xslt);
        var row = Assert.Single(Detect(result.Xslt).Rows);
        Assert.Equal("A<&>B", row.BankName);
        Assert.False(row.BankNameEditable);
    }

    [Fact]
    public void PreservesXslAndImages()
    {
        var xslt = Theme($"""
                <img src="{Png}" data-xc-placeholder="logo"/>
                <table>
                  <tr><td>Ziraat</td><td>{Iban1}</td></tr>
                  <tr><td>Halk</td><td>{Iban2}</td></tr>
                </table>
                <xsl:if test="//cbc:Note"><div><xsl:value-of select="//cbc:Note"/></div></xsl:if>
        """);

        var result = Apply(xslt, Remove(0), Edit(1, "Halkbank", Iban3), Edit(null, "Akbank", Iban1));

        AssertValid(xslt, result);
        Assert.Contains("data-xc-placeholder=\"logo\"", result.Xslt);
        Assert.Equal(["Halkbank", "Akbank"], Detect(result.Xslt).Rows.Select(r => r.BankName));
    }

    [Fact]
    public void RemoveAll_PlainTable_TableRemoved()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Remove(0), Remove(1));

        AssertValid(xslt, result);
        Assert.DoesNotContain("<table", result.Xslt);
        Assert.DoesNotContain("Banka", result.Xslt);
        Assert.False(Detect(result.Xslt).Found);
    }

    [Fact]
    public void RemoveAll_TableWithXsl_OnlyRowsRemoved()
    {
        var xslt = TwoColumnTable("""
            <tr><td>Şube</td><td><xsl:value-of select="//cbc:ID"/></td></tr>
            """);

        var result = Apply(xslt, Remove(0), Remove(1));

        AssertValid(xslt, result);
        Assert.Contains("<table id=\"banks\">", result.Xslt);
        Assert.Contains("<th>Banka</th>", result.Xslt);
        Assert.DoesNotContain(Iban1, result.Xslt);
        Assert.DoesNotContain(Iban2, result.Xslt);
    }

    [Fact]
    public void RemoveAll_WithAdd_TableKept()
    {
        var xslt = TwoColumnTable();

        var result = Apply(xslt, Remove(0), Remove(1), Edit(null, "Akbank", Iban3));

        AssertValid(xslt, result);
        var row = Assert.Single(Detect(result.Xslt).Rows);
        Assert.Equal("Akbank", row.BankName);
        Assert.Equal(Iban3, row.Iban);
    }

    [Fact]
    public void NotFound_Warns()
    {
        var xslt = Theme("<div>Banka yok</div>");

        var result = Apply(xslt, Edit(0, "A", Iban1));

        Assert.False(result.Changed);
        Assert.Same(xslt, result.Xslt);
        Assert.Equal(DocumentBankEditor.NotFound, Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public void SkippedRequests_Warn()
    {
        var locked = TwoColumnTable("""
            <tr><td>Akbank</td><td>TR120001000000000000000001</td><td><div data-xc-slot="bank-box" data-xc-inserted="1"></div></td></tr>
            """);

        var result = Apply(locked, Edit(7, "X", Iban1), Remove(2), Edit(null, "Y", Iban1));

        Assert.False(result.Changed);
        Assert.Equal(
            [DocumentBankEditor.RowNotFound, DocumentBankEditor.RowLocked, DocumentBankEditor.AddUnsupported],
            result.Warnings.Select(w => w.Code));
        Assert.All(result.Warnings, w => Assert.Null(w.Kind));
    }

    [Fact]
    public void LockedName_Warns()
    {
        var xslt = Theme($"<div>A&amp;B IBAN: {Iban1}</div>");

        var result = Apply(xslt, Edit(0, "Yeni", Iban1));

        Assert.False(result.Changed);
        Assert.Equal(DocumentBankEditor.RowLocked, Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public void Limit_Warns()
    {
        var xslt = TwoColumnTable();
        var adds = Enumerable.Range(0, 10).Select(_ => Edit(null, "Ek", Iban3)).ToArray();

        var result = Apply(xslt, adds);

        Assert.Equal(10, Detect(result.Xslt).Rows.Count);
        Assert.Equal(2, result.Warnings.Count(w => w.Code == DocumentBankEditor.Limit));
    }
}
