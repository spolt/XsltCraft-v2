using System.Text;
using System.Xml;
using net.sf.saxon.s9api;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Input;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>G0 O-7: köprü overload'ları, sınırlar ve hata durumunda kaynak kapatma.</summary>
public sealed class XmlReaderTreeBridgeTests
{
    private const string Sample = """<?xml version="1.0"?><inv:r xmlns:inv="urn:inv" xmlns="urn:def" xml:lang="tr" a="1"><x b="ş"/><!--c--><?pi d?><![CDATA[<t>]]> metin</inv:r>""";

    private static readonly TreeBridgeLimits Limits = new(64, 10_000, 1_000_000);

    private static Processor NewProcessor() => new SaxonProcessorFactory(SaxonEngineOptions.Isolated).CreateProcessor();

    [Fact]
    public void StreamAndReaderOverloads()
    {
        var processor = NewProcessor();
        var bridge = new XmlReaderTreeBridge(processor);

        var fromString = bridge.Build(Sample, Limits);
        var fromStream = bridge.Build(new MemoryStream(Encoding.UTF8.GetBytes(Sample)), Limits);
        using var reader = XmlReader.Create(new StringReader(Sample), SecureXslt.ReaderSettings());
        var fromReader = bridge.Build(reader, Limits);

        // Saxon'un kendi ayrıştırıcısıyla aynı ağaç (kontrol).
        var viaSaxon = processor.newDocumentBuilder().build(
            new javax.xml.transform.stream.StreamSource(new java.io.StringReader(Sample), "urn:x"));

        var expected = viaSaxon.toString();
        Assert.Equal(expected, fromString.toString());
        Assert.Equal(expected, fromStream.toString());
        Assert.Equal(expected, fromReader.toString());
        Assert.Contains("xml:lang=\"tr\"", expected);
        Assert.Contains("&lt;t&gt; metin", expected);
    }

    [Fact]
    public void ReaderOverload_RequiresProhibitDtd()
    {
        var bridge = new XmlReaderTreeBridge(NewProcessor());
        using var unsafeReader = XmlReader.Create(new StringReader("<r/>"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        Assert.Throws<ArgumentException>(() => bridge.Build(unsafeReader, Limits));
    }

    [Fact]
    public void MaxDepth()
    {
        var bridge = new XmlReaderTreeBridge(NewProcessor());
        bridge.Build(Deep("a", 64), Limits);

        var ex = Assert.Throws<XsltProcessingException>(() => bridge.Build(Deep("a", 65), Limits));
        Assert.Equal(XsltFailureKind.Xml, ex.Diagnostic.Kind);
        Assert.Equal(XsltEngineKind.Saxon, ex.Diagnostic.Engine);
        Assert.True(ex.Diagnostic.Line > 0);
    }

    [Fact]
    public void MaxNodes_And_MaxCharacters()
    {
        var bridge = new XmlReaderTreeBridge(NewProcessor());
        var wide = "<r>" + string.Concat(Enumerable.Repeat("<i a=\"1\"/>", 6000)) + "</r>";
        Assert.Throws<XsltProcessingException>(() => bridge.Build(wide, Limits));

        var big = "<r>" + new string('x', 2_000_000) + "</r>";
        Assert.Throws<XmlException>(() => bridge.Build(big, Limits));
    }

    [Fact]
    public void Dtd_Rejected()
    {
        var bridge = new XmlReaderTreeBridge(NewProcessor());
        Assert.Throws<XmlException>(() => bridge.Build("""<!DOCTYPE r [<!ENTITY e "x">]><r>&e;</r>""", Limits));
    }

    [Fact]
    public void DisposesOnError()
    {
        var bridge = new XmlReaderTreeBridge(NewProcessor());
        var stream = new TrackingStream(Encoding.UTF8.GetBytes(Deep("a", 100)));

        Assert.Throws<XsltProcessingException>(() => bridge.Build(stream, Limits));
        Assert.True(stream.Disposed, "akış hata sonrası kapatılmadı");

        var malformed = new TrackingStream(Encoding.UTF8.GetBytes("<r><a></r>"));
        Assert.Throws<XmlException>(() => bridge.Build(malformed, Limits));
        Assert.True(malformed.Disposed);

        // Hata sonrası köprü/processor tekrar kullanılabilir.
        Assert.NotNull(bridge.Build("<ok/>", Limits));
    }

    private sealed class TrackingStream(byte[] data) : MemoryStream(data)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
