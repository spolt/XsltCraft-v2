using System.Xml.Linq;

using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.Tests.Xslt;

/// <summary>
/// Sabit not enjeksiyonunun saf (DB'siz) doğrulaması: koşullu/koşulsuz döngü ayrımı, tek-kez
/// render, XML kaçışı, idempotent değiştirme, ekleme modu ve İrsaliye desteği.
/// </summary>
public class FixedNoteInjectorTests
{
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";
    private readonly FixedNoteInjector _sut = new();

    // İlk for-each xsl:choose içinde (koşullu SGK benzeri) → dokunulmamalı.
    // İkinci for-each koşulsuz (içte filtre xsl:if'i var) → hedef.
    private const string InvoiceXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
          xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <xsl:template match="/">
            <html><body>
              <xsl:choose>
                <xsl:when test="//n1:Invoice/cbc:Note[starts-with(.,'SGK_')]">
                  <xsl:for-each select="//n1:Invoice/cbc:Note">
                    <span><xsl:value-of select="."/></span>
                  </xsl:for-each>
                </xsl:when>
              </xsl:choose>
              <xsl:for-each select="//n1:Invoice/cbc:Note">
                <xsl:if test="not(starts-with(.,'SGK_'))">
                  <b> Not: </b>
                  <xsl:value-of select="."/>
                  <br/>
                </xsl:if>
              </xsl:for-each>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string DespatchXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <xsl:template match="/">
            <table>
              <xsl:for-each select="//n1:DespatchAdvice/cbc:Note">
                <tr><td><b>Not:</b><xsl:value-of select="."/></td></tr>
              </xsl:for-each>
            </table>
          </xsl:template>
        </xsl:stylesheet>
        """;

    // Yalnız koşullu (choose içinde) not döngüsü → tutturacak koşulsuz döngü yok.
    private const string OnlyConditionalXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <xsl:template match="/">
            <html><body>
              <xsl:if test="true()">
                <xsl:for-each select="//n1:Invoice/cbc:Note">
                  <span><xsl:value-of select="."/></span>
                </xsl:for-each>
              </xsl:if>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private static IEnumerable<XElement> NoteTexts(string xslt) =>
        XDocument.Parse(xslt).Descendants(Xsl + "text");

    [Fact]
    public void Injects_after_unconditional_loop_exactly_once_and_outside_conditions()
    {
        var result = _sut.Inject(InvoiceXslt, "Sabit dipnot", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        Assert.Contains("xc:fixed-note:start", result.Xslt);

        // Sonuç iyi-biçimli kalmalı.
        var doc = XDocument.Parse(result.Xslt);

        // Sabit not tam bir kez, üstelik bir for-each'in DIŞINDA (tek kez render) ve
        // herhangi bir if/when/choose ALTINDA olmadan yer almalı.
        var injected = doc.Descendants(Xsl + "text").Where(t => t.Value == "Sabit dipnot").ToList();
        Assert.Single(injected);

        var ancestors = injected[0].Ancestors().Where(a => a.Name.Namespace == Xsl).Select(a => a.Name.LocalName).ToList();
        Assert.DoesNotContain("for-each", ancestors);
        Assert.DoesNotContain("if", ancestors);
        Assert.DoesNotContain("when", ancestors);
        Assert.DoesNotContain("choose", ancestors);

        // Koşullu döngü olduğu gibi durmalı (hâlâ iki for-each).
        Assert.Equal(2, doc.Descendants(Xsl + "for-each").Count());
    }

    [Fact]
    public void Escapes_xml_special_characters_in_note_text()
    {
        const string note = "A & B < C > \"Ç\"";
        var result = _sut.Inject(InvoiceXslt, note, FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        Assert.Contains("&amp;", result.Xslt);
        Assert.Contains("&lt;", result.Xslt);
        // Ham (kaçışsız) enjeksiyon olmamalı.
        Assert.DoesNotContain("A & B", result.Xslt);

        // Yeniden ayrıştırınca metin aynen geri gelmeli.
        var doc = XDocument.Parse(result.Xslt);
        Assert.Contains(doc.Descendants(Xsl + "text"), t => t.Value == note);
    }

    [Fact]
    public void Replace_mode_is_idempotent_and_updates_text()
    {
        var first = _sut.Inject(InvoiceXslt, "Birinci", FixedNoteMode.Replace);
        var second = _sut.Inject(first.Xslt, "İkinci", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, second.Status);
        // Tek marker bloğu kalmalı, yeni for-each enjekte edilmemeli.
        Assert.Equal(1, CountOccurrences(second.Xslt, "xc:fixed-note:start"));
        Assert.DoesNotContain("Birinci", second.Xslt);
        Assert.Contains("İkinci", second.Xslt);

        var doc = XDocument.Parse(second.Xslt);
        Assert.Equal(2, doc.Descendants(Xsl + "for-each").Count()); // başlangıçtaki iki döngü
    }

    [Fact]
    public void Append_mode_keeps_existing_and_adds_new_line()
    {
        var first = _sut.Inject(InvoiceXslt, "Birinci", FixedNoteMode.Replace);
        var second = _sut.Inject(first.Xslt, "İkinci", FixedNoteMode.Append);

        Assert.Equal(FixedNoteStatus.Updated, second.Status);
        Assert.Equal(1, CountOccurrences(second.Xslt, "xc:fixed-note:start"));
        Assert.Contains("Birinci", second.Xslt);
        Assert.Contains("İkinci", second.Xslt);

        var doc = XDocument.Parse(second.Xslt);
        var texts = doc.Descendants(Xsl + "text").Select(t => t.Value).ToList();
        Assert.Contains("Birinci", texts);
        Assert.Contains("İkinci", texts);
    }

    [Fact]
    public void Derives_table_styling_for_despatch_advice()
    {
        var result = _sut.Inject(DespatchXslt, "İrsaliye notu", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        var doc = XDocument.Parse(result.Xslt);

        var injected = doc.Descendants(Xsl + "text").Single(t => t.Value == "İrsaliye notu");
        // Şablonun kendi <tr><td> stilini korumalı.
        Assert.NotNull(injected.Ancestors("td").FirstOrDefault());
        Assert.NotNull(injected.Ancestors("tr").FirstOrDefault());
        Assert.Empty(injected.Ancestors(Xsl + "for-each"));
    }

    [Fact]
    public void Returns_no_notes_section_when_only_conditional_loops_exist()
    {
        var result = _sut.Inject(OnlyConditionalXslt, "Olmaz", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.NoNotesSection, result.Status);
        Assert.Equal(OnlyConditionalXslt, result.Xslt); // değiştirilmemeli
    }

    [Fact]
    public void Returns_failed_for_malformed_xslt()
    {
        var result = _sut.Inject("<xsl:stylesheet><not-closed>", "x", FixedNoteMode.Replace);
        Assert.Equal(FixedNoteStatus.Failed, result.Status);
    }

    // İlk not döngüsü bir xsl:variable içinde (SGK değer çıkarımı); gerçek gösterim döngüsü gövdede.
    private const string VariableLoopXslt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="2.0"
          xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
          xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
          xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <xsl:template match="/">
            <xsl:variable name="vareczanehizmetbedeli">
              <xsl:for-each select="//n1:Invoice/cbc:Note">
                <xsl:choose>
                  <xsl:when test="substring(.,0,9) = 'SGK_EHB:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                  </xsl:when>
                </xsl:choose>
              </xsl:for-each>
            </xsl:variable>
            <html><body>
              <div class="notes">
                <xsl:for-each select="//n1:Invoice/cbc:Note">
                  <p><b>Not: </b><xsl:value-of select="."/></p>
                </xsl:for-each>
              </div>
            </body></html>
          </xsl:template>
        </xsl:stylesheet>
        """;

    [Fact]
    public void Skips_note_loops_inside_xsl_variable()
    {
        var result = _sut.Inject(VariableLoopXslt, "Gövde notu", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        var doc = XDocument.Parse(result.Xslt);

        var injected = doc.Descendants(Xsl + "text").Single(t => t.Value == "Gövde notu");
        // Not, değer-üretimi yapan xsl:variable'ın İÇİNDE değil, gövdedeki gösterim döngüsünün yanında olmalı.
        Assert.Empty(injected.Ancestors(Xsl + "variable"));
        Assert.Empty(injected.Ancestors(Xsl + "for-each"));
        Assert.Equal(2, doc.Descendants(Xsl + "for-each").Count()); // değişken + gövde döngüleri korunur
    }

    [Fact]
    public void Self_heals_note_previously_injected_into_wrong_location()
    {
        // Eski (hatalı) davranış: marker bloğu, xsl:variable içindeki İLK döngüden hemen sonra gömülmüş.
        const string endTag = "</xsl:for-each>";
        var afterFirstLoop = VariableLoopXslt.IndexOf(endTag, StringComparison.Ordinal) + endTag.Length;
        var wronglyInjected = VariableLoopXslt.Insert(afterFirstLoop,
            "\n<!-- xc:fixed-note:start -->\n<b> Not: </b><xsl:text>ESKI</xsl:text><br />\n<!-- xc:fixed-note:end -->");
        Assert.Contains("xc:fixed-note:start", wronglyInjected); // ön koşul: gerçekten yanlış yerde

        var result = _sut.Inject(wronglyInjected, "YENI", FixedNoteMode.Replace);

        Assert.Equal(FixedNoteStatus.Updated, result.Status);
        Assert.Equal(1, CountOccurrences(result.Xslt, "xc:fixed-note:start")); // tek blok
        Assert.DoesNotContain("ESKI", result.Xslt);
        Assert.Contains("YENI", result.Xslt);

        var doc = XDocument.Parse(result.Xslt);
        var injected = doc.Descendants(Xsl + "text").Single(t => t.Value == "YENI");
        Assert.Empty(injected.Ancestors(Xsl + "variable")); // artık değişken içinde değil → gövdede görünür
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
