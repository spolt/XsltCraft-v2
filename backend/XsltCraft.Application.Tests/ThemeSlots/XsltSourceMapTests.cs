using XsltCraft.Application.ThemeSlots.Source;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class XsltSourceMapTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Spans_match_source_for_any_line_ending(string nl)
    {
        var source = string.Join(nl, "<root>", "  <a x=\"1 > 0\">t</a>", "\t<b/>", "  <!-- c -->", "  <c><![CDATA[<x>]]></c>", "</root>");
        Assert.True(XsltSourceMap.TryBuild(source, out var map, out var error), error);

        foreach (var e in map.Document.Descendants())
        {
            var span = map.Span(e);
            Assert.Equal('<', source[span.Start]);
            Assert.Equal('>', source[span.StartTagEnd - 1]);
            Assert.Equal('>', source[span.End - 1]);
            if (!span.SelfClosing)
                Assert.Equal("</" + e.Name.LocalName, source.Substring(span.EndTagStart, e.Name.LocalName.Length + 2));
        }

        var a = map.Span(map.Document.Root!.Element("a")!);
        Assert.Equal("<a x=\"1 > 0\">", source[a.Start..a.StartTagEnd]);
        Assert.True(map.Span(map.Document.Root!.Element("b")!).SelfClosing);
    }

    [Fact]
    public void Edits_are_applied_back_to_front_and_keep_insertion_order()
    {
        var edits = new SourceEdits();
        edits.Insert(3, "X");
        edits.Insert(3, "Y");
        edits.Replace(0, 1, "_");
        Assert.Equal("_bcXYdef", edits.Apply("abcdef"));
    }

    [Fact]
    public void Overlapping_edits_fail()
    {
        var edits = new SourceEdits();
        edits.Replace(0, 3, "X");
        edits.Replace(2, 4, "Y");
        Assert.Throws<InvalidOperationException>(() => edits.Apply("abcdef"));
    }
}
