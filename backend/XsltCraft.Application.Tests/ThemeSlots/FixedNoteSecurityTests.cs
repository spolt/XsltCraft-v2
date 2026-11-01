using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using XsltCraft.Application.DTO;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>Tema Kullan sabit notları: metin yalnız text node, stil beyaz listesi, Notlar alanında yerleşim, idempotentlik.</summary>
public class FixedNoteSecurityTests
{
    private static readonly IThemeAssetComposer Composer = new ThemeAssetComposer(new ThemeImageValidator());
    // Fixture'daki eski görsel bloğu, görselsiz compose'da zaten silinir; karşılaştırma bloksuz temayla.
    private static readonly Lazy<string> Theme = new(() => OverridesBlockText.RemoveAll(ThemeFixtures.Load(ThemeFixtures.TestTheme)));

    private static FixedNoteInput Note(FixedNotePosition position, string text) =>
        new(position, text, true, true, true, 12, "#1A2B3C", "times", FixedNoteAlign.Center);

    private static ThemeComposeResult Compose(string xslt, params FixedNoteInput[] notes) =>
        Composer.Compose(xslt, new ThemeComposeInput([], [], FixedNotes: notes));

    [Theory]
    [InlineData("<xsl:value-of select=\"document('x')\"/>", "&lt;xsl:value-of select=\"document('x')\"/&gt;")]
    [InlineData("{concat(1,2)}", "{concat(1,2)}")]
    [InlineData("a ]]> b", "a ]]&gt; b")]
    [InlineData("&amp;", "&amp;amp;")]
    [InlineData("</div><script>alert(1)</script>", "&lt;/div&gt;&lt;script&gt;alert(1)&lt;/script&gt;")]
    public void Malicious_text_is_written_as_plain_text(string text, string expectedHtml)
    {
        var xml = ThemeFixtures.Samples.Get(DocumentType.Invoice);
        var baseline = ThemeFixtures.Renderer.Render(Theme.Value, xml).Html!;

        var result = Compose(Theme.Value, Note(FixedNotePosition.Before, text));

        Assert.True(result.Success, result.Error);
        SecureXslt.Compile(result.Xslt);
        Assert.Equal(Count(Theme.Value, "<xsl:"), Count(result.Xslt, "<xsl:"));
        Assert.Equal(Count(Theme.Value, "<!--"), Count(result.Xslt, "<!--"));

        var html = ThemeFixtures.Renderer.Render(result.Xslt, xml).Html!;
        Assert.Contains(expectedHtml, html);
        Assert.Equal(Count(baseline, "<script"), Count(html, "<script"));
        Assert.Contains(
            @"style=""margin:2px 0;font-size:12px;color:#1a2b3c;font-family:'Times New Roman',Times,serif;text-align:center;font-weight:bold;font-style:italic;text-decoration:underline;white-space:pre-wrap""",
            html);
    }

    [Theory]
    [InlineData("color", "#fff")]
    [InlineData("color", "#000000;background:url(x)")]
    [InlineData("color", "#00000{}")]
    [InlineData("color", "#000000\n")]
    [InlineData("fontFamily", "comic")]
    [InlineData("fontFamily", "Arial")]
    [InlineData("fontSize", "99")]
    [InlineData("align", "justify")]
    [InlineData("position", "middle")]
    [InlineData("text", "a\u0001b")]
    [InlineData("text", "a\tb")]
    [InlineData("text", "501")]
    [InlineData("text", "11-lines")]
    [InlineData("list", "3-notes")]
    [InlineData("list", "same-position")]
    [InlineData("list", "null-item")]
    public void Validator_rejects_invalid_notes(string what, string value)
    {
        var r = Request();
        var n = r.FixedNotes![0];
        switch (what)
        {
            case "color": n.Color = value; break;
            case "fontFamily": n.FontFamily = value; break;
            case "fontSize": n.FontSize = int.Parse(value); break;
            case "align": n.Align = value; break;
            case "position": n.Position = value; break;
            case "text":
                n.Text = value switch
                {
                    "501" => new string('a', 501),
                    "11-lines" => string.Join('\n', Enumerable.Repeat("x", 11)),
                    _ => value,
                };
                break;
            case "list" when value == "3-notes": r.FixedNotes.Add(new ThemeComposeFixedNoteDto { Position = "after", Text = "x" }); break;
            case "list" when value == "same-position": r.FixedNotes[1].Position = "before"; break;
            case "list": r.FixedNotes.Add(null!); break;
        }

        Assert.Equal("fixedNotes", ThemeComposeRequestValidator.Validate(r)?.Field);
    }

    [Fact]
    public void Validator_accepts_whitelisted_notes()
    {
        var r = Request();
        r.FixedNotes![0].Text = new string('a', 500);
        r.FixedNotes[1].Text = string.Join('\n', Enumerable.Repeat("x", 10));
        Assert.Null(ThemeComposeRequestValidator.Validate(r));
    }

    [Fact]
    public void Notes_are_placed_inside_notes_cell_around_note_rows()
    {
        var result = Compose(Theme.Value, Note(FixedNotePosition.After, "ALT NOT"), Note(FixedNotePosition.Before, "UST NOT"));

        Assert.True(result.Success, result.Error);
        Assert.True(result.FixedNotesSupported);
        var x = result.Xslt;
        var td = x.IndexOf("id=\"notesTableTd\"", StringComparison.Ordinal);
        var before = x.IndexOf("data-xc-fixed-note=\"before\"", StringComparison.Ordinal);
        var firstNote = x.IndexOf("Not: </b>", td, StringComparison.Ordinal);
        var payment = x.IndexOf("Ödeme Koşulu", td, StringComparison.Ordinal);
        var after = x.IndexOf("data-xc-fixed-note=\"after\"", StringComparison.Ordinal);

        Assert.True(td < before && before < firstNote, "üst not ilk 'Not:' satırından önce, Notlar hücresinde olmalı");
        Assert.True(payment < after, "alt not Ödeme Koşulu'ndan sonra olmalı");
        Assert.True(x.IndexOf("<td", td + 1, StringComparison.Ordinal) > after, "alt not Notlar hücresinde olmalı");
        Assert.Contains("ALT NOT</div></td>", x); // hücrenin içerik sonu
        Assert.Contains("UST NOT</div><xsl:for-each select=\"//n1:Invoice/cbc:Note\">", x); // döngünün hemen öncesi
        SecureXslt.Compile(x);
    }

    [Fact]
    public void Theme_without_notes_area_is_unsupported_and_unchanged()
    {
        const string xslt = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">
              <xsl:output method="html"/>
              <xsl:template match="/"><html><body><div>Fatura</div></body></html></xsl:template>
            </xsl:stylesheet>
            """;

        var result = Compose(xslt, Note(FixedNotePosition.Before, "Not"));

        Assert.True(result.Success, result.Error);
        Assert.False(result.FixedNotesSupported);
        Assert.Equal(xslt, result.Xslt);
        Assert.Contains(result.Warnings, w => w.Code == "notes-not-found");
    }

    [Fact]
    public void Applying_twice_keeps_single_copy_and_empty_list_removes()
    {
        var notes = new[] { Note(FixedNotePosition.Before, "Bir\nİki"), Note(FixedNotePosition.After, "Son") };
        var once = Compose(Theme.Value, notes).Xslt;
        var twice = Compose(once, notes).Xslt;

        Assert.Equal(once, twice);
        Assert.Equal(1, Count(twice, "data-xc-fixed-note=\"before\""));
        Assert.Equal(1, Count(twice, "data-xc-fixed-note=\"after\""));
        Assert.Contains("Bir<br />İki", once);

        Assert.Equal(Theme.Value, Compose(once).Xslt);
    }

    [Fact]
    public void Nested_marker_flood_under_input_limits_fails_closed_in_bounded_time()
    {
        // Saldırı biçimi, yapısal ön denetimin altına indirilmiş: fixedNotes:[] ile M²×D ata taraması tetiklerdi.
        const int depth = 200, markers = 10_000;
        var xslt = string.Concat(Enumerable.Repeat("<b>", depth))
            + string.Concat(Enumerable.Repeat("<a data-xc-fixed-note=\"1\"/>", markers))
            + string.Concat(Enumerable.Repeat("</b>", depth));
        Assert.Null(ThemeComposeXsltLimits.Check(xslt));

        var sw = Stopwatch.StartNew();
        var result = Compose(xslt);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Süre: {sw.Elapsed}");
        Assert.False(result.Success);
        Assert.Equal(xslt, result.Xslt);
    }

    [Fact]
    public void Structural_limits_accept_largest_real_themes_and_leave_malformed_text_to_composer()
    {
        var themes = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "ThemeSlots", "Fixtures"), "*.xslt")
            .Concat(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", "XsltImport", "Resources", "Tier_B_RealWorld"), "*.xslt"))
            .ToList();
        Assert.Equal(9, themes.Count);
        Assert.All(themes, path => Assert.Null(ThemeComposeXsltLimits.Check(File.ReadAllText(path))));

        Assert.Null(ThemeComposeXsltLimits.Check("<a><b></a>"));
        Assert.Null(ThemeComposeXsltLimits.Check("bu xml degil"));

        // Metin, tırnak içi, yorum, CDATA ve PI öznitelik sayılmaz.
        var equalsSigns = string.Concat(Enumerable.Repeat("x=1 ", 5_000));
        Assert.Null(ThemeComposeXsltLimits.Check(
            $"<?pi {equalsSigns}?><r a='{equalsSigns}' b=\"> {equalsSigns}\">{equalsSigns}<!-- <a {equalsSigns}> --><![CDATA[<a {equalsSigns}>]]></r>"));
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static ThemeComposeRequest Request() => new()
    {
        Xslt = "<x/>",
        IncludeXslt = true,
        FixedNotes =
        [
            new ThemeComposeFixedNoteDto { Position = "before", Text = "Üst", FontSize = 12, Color = "#112233", FontFamily = "arial", Align = "left" },
            new ThemeComposeFixedNoteDto { Position = "after", Text = "Alt", FontSize = 18, Color = "#aabbcc", FontFamily = "theme", Align = "right" },
        ],
    };

    private static int Count(string s, string value) => Regex.Matches(s, Regex.Escape(value)).Count;
}
