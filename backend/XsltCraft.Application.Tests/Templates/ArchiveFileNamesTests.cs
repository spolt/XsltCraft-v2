using System.IO.Compression;
using System.Text;

using XsltCraft.Application.Templates.Download;

namespace XsltCraft.Application.Tests.Templates;

/// <summary>ZIP girdi adı temizleme (Zip Slip / Windows) ve <see cref="TemplateArchiveWriter"/> uçtan uca okuma.</summary>
public class ArchiveFileNamesTests
{
    [Theory]
    [InlineData("../../etc/passwd", "_._etc_passwd.xslt")]
    [InlineData("a/b\\c", "a_b_c.xslt")]
    [InlineData("CON", "_CON.xslt")]
    [InlineData("con.xslt", "_con.xslt")]
    [InlineData("LPT1 .xsl", "_LPT1.xsl")]
    [InlineData("", "sablon.xslt")]
    [InlineData(null, "sablon.xslt")]
    [InlineData("   ", "sablon.xslt")]
    [InlineData(" . .. . ", "sablon.xslt")]
    [InlineData("ab‮cd", "ab_cd.xslt")]
    [InlineData("Çağrı Şubat İrsaliye ğüşöı", "Çağrı Şubat İrsaliye ğüşöı.xslt")]
    [InlineData("Fatura.xsl", "Fatura.xsl")]
    public void ToEntryName_UnsafeOrEdgeName_ReturnsExpectedSafeName(string? raw, string expected)
    {
        var name = ArchiveFileNames.ToEntryName(raw);

        Assert.Equal(expected, name);
        AssertSafe(name);
    }

    [Fact]
    public void ToEntryName_300Chars_TruncatesStemTo120()
    {
        var name = ArchiveFileNames.ToEntryName(new string('a', 300));

        Assert.Equal(new string('a', ArchiveFileNames.MaxStemLength) + ".xslt", name);
        AssertSafe(name);
    }

    [Fact]
    public void ToEntryName_EmojiOnTruncationBoundary_DoesNotSplitSurrogatePair()
    {
        // Emoji UTF-16'da 119-120. birimlerde: kesim çiftin ortasına düşer → emoji tamamen atılır.
        var name = ArchiveFileNames.ToEntryName(new string('a', 119) + "\U0001F600" + "bbb");

        Assert.Equal(new string('a', 119) + ".xslt", name);
        Assert.DoesNotContain(name, char.IsSurrogate);
        AssertSafe(name);
    }

    [Fact]
    public void Reserve_CaseInsensitiveDuplicates_AppendsSequenceNumbers()
    {
        var registry = new ArchiveNameRegistry();

        var names = new[] { "Fatura", "fatura", "FATURA.xslt" }.Select(registry.Reserve).ToList();

        Assert.Equal(["Fatura.xslt", "fatura (2).xslt", "FATURA (3).xslt"], names);
        Assert.All(names, AssertSafe);
    }

    [Fact]
    public async Task WriteAsync_DuplicateAndTurkishEntries_ZipHasUniqueSafeNamesAndExactBytesWithoutBom()
    {
        var sources = new[]
        {
            new ArchiveEntrySource("Fatura", "<a>bir</a>", new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)),
            new ArchiveEntrySource("fatura", "<a>iki</a>", null),
            new ArchiveEntrySource("İrsaliye Şablonu", "<xsl:text>çğıöşü ÇĞİÖŞÜ</xsl:text>", new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        };
        using var buffer = new MemoryStream();

        await new TemplateArchiveWriter().WriteAsync(ToAsync(sources), buffer, CancellationToken.None);

        buffer.Position = 0;
        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        Assert.Equal(["Fatura.xslt", "fatura (2).xslt", "İrsaliye Şablonu.xslt"], zip.Entries.Select(e => e.FullName));

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        for (var i = 0; i < sources.Length; i++)
        {
            var entry = zip.Entries[i];
            AssertSafe(entry.FullName);
            Assert.Equal(entry.Name, entry.FullName); // dizin bileşeni yok

            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var actual = bytes.ToArray();

            Assert.Equal(utf8NoBom.GetBytes(sources[i].Content), actual);
            Assert.False(actual.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "BOM yazılmamalı");
        }
    }

    private static void AssertSafe(string name)
    {
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("\\", name);
        Assert.DoesNotContain("..", name);
        Assert.False(name.StartsWith("..", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    private static async IAsyncEnumerable<ArchiveEntrySource> ToAsync(IEnumerable<ArchiveEntrySource> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
