using net.sf.saxon;
using net.sf.saxon.lib;
using net.sf.saxon.s9api;
using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// G1 security-reviewer bağlayıcı şartı: <c>http://saxon.sf.net/collation?class=X</c> ALLOW_EXTERNAL_FUNCTIONS=false
/// iken bile classpath'ten sınıf yükleyip sıfır-argümanlı kurucusunu çağırır. Statik tarama
/// <c>concat(...)</c> ile kurulan URI'yi göremez → kapı <see cref="RestrictedCollationResolver"/>.
/// Hem statik (derleme) hem dinamik (çalışma) yolları ve Saxon'un collation kullanan tüm giriş noktaları sınanır.
/// </summary>
public sealed class SaxonCollationTests(ITestOutputHelper output)
{
    private const string ObjectClassCollation = "http://saxon.sf.net/collation?class=java.lang.Object";
    private const string DynObject = "concat('http://saxon','.sf.net/collation?class=java.lang.Object')";

    public static TheoryData<string, string> DeniedVectors => new()
    {
        { "compare dynamic concat", $"<xsl:value-of select=\"compare('a','b', {DynObject})\"/>" },
        { "compare literal class", $"<xsl:value-of select=\"compare('a','b', '{ObjectClassCollation}')\"/>" },
        { "compare dynamic via variable", $"<xsl:variable name=\"c\" select=\"{DynObject}\"/><xsl:value-of select=\"compare('a','b', $c)\"/>" },
        { "compare dynamic from input", "<xsl:value-of select=\"compare('a','b', string(/r/@c))\"/>" },
        { "xsl:sort collation AVT dynamic", $"<xsl:for-each select=\"('b','a')\"><xsl:sort select=\".\" collation=\"{{{DynObject}}}\"/><xsl:value-of select=\".\"/></xsl:for-each>" },
        { "xsl:sort collation literal", $"<xsl:for-each select=\"('b','a')\"><xsl:sort select=\".\" collation=\"{ObjectClassCollation}\"/><xsl:value-of select=\".\"/></xsl:for-each>" },
        { "for-each-group collation", $"<xsl:for-each-group select=\"('a','b')\" group-by=\".\" collation=\"{ObjectClassCollation}\"><xsl:value-of select=\".\"/></xsl:for-each-group>" },
        { "distinct-values dynamic", $"<xsl:value-of select=\"distinct-values(('a','b'), {DynObject})\"/>" },
        { "deep-equal dynamic", $"<xsl:value-of select=\"deep-equal(('a'), ('a'), {DynObject})\"/>" },
        { "contains dynamic", $"<xsl:value-of select=\"contains('ab', 'b', {DynObject})\"/>" },
        { "starts-with dynamic", $"<xsl:value-of select=\"starts-with('ab', 'a', {DynObject})\"/>" },
        { "index-of dynamic", $"<xsl:value-of select=\"index-of(('a'), 'a', {DynObject})\"/>" },
        { "max dynamic", $"<xsl:value-of select=\"max(('a','b'), {DynObject})\"/>" },
        { "UCA with percent literal", "<xsl:value-of select=\"compare('a','b','http://www.w3.org/2013/collation/UCA?lang=tr%2DTR')\"/>" },
        { "UCA with percent dynamic", "<xsl:value-of select=\"compare('a','b', concat('http://www.w3.org/2013/collation/UCA?lang=tr', '%2DTR'))\"/>" },
        { "UCA with class param", "<xsl:value-of select=\"compare('a','b','http://www.w3.org/2013/collation/UCA?class=java.lang.Object')\"/>" },
        { "saxon collation without class", "<xsl:value-of select=\"compare('a','b','http://saxon.sf.net/collation?lang=tr')\"/>" },
    };

    [Theory]
    [MemberData(nameof(DeniedVectors))]
    public void CollationVector_Rejected_ByResolver(string name, string body)
    {
        var xml = $"<r c=\"{ObjectClassCollation}\"/>";
        var d = Fail(Stylesheet(body), xml);
        output.WriteLine($"{name}: {d.Kind}/{d.ErrorCode} line={d.Line}: {d.Message}");

        Assert.Equal(XsltFailureKind.Security, d.Kind);
        Assert.Equal(SandboxCodes.CollationDenied, d.ErrorCode);
    }

    [Fact]
    public void DynamicCollationClass_ViaConcat_Rejected_NoClassLoaded()
    {
        // (1) Gerçek motor: hata BİZİM çözümleyicimizden (kod) gelir.
        var d = Fail(Stylesheet($"<xsl:value-of select=\"compare('a','b', {DynObject})\"/>"));
        Assert.Equal(SandboxCodes.CollationDenied, d.ErrorCode);

        // (2) Reddedilen URI'de standart çözümleyici (sınıf yükleyen kod) HİÇ çağrılmaz.
        var recording = new RecordingCollationResolver();
        var restricted = new RestrictedCollationResolver(recording);
        var config = new Processor(false).getUnderlyingConfiguration();
        foreach (var uri in new[] { ObjectClassCollation, "http://saxon.sf.net/collation?class=cli.System.Object",
                     "http://www.w3.org/2013/collation/UCA?class=x", "http://www.w3.org/2013/collation/UCA?lang=tr%2DTR", "urn:x" })
        {
            Assert.Throws<SandboxViolationException>(() => restricted.resolve(uri, config));
        }

        Assert.Empty(recording.Calls);

        // (3) Kurulumun gerçekten config düzeyinde olduğu (tüm giriş noktaları Configuration.getCollation'dan geçer).
        var processor = new SaxonProcessorFactory(SaxonEngineOptions.Isolated).CreateProcessor();
        Assert.IsType<RestrictedCollationResolver>(processor.getUnderlyingConfiguration().getCollationURIResolver());
    }

    [Fact]
    public void Control_DefaultSaxon_InstantiatesCollationClass()
    {
        // Kontrol: sandbox'sız (AEF=false bile) Saxon java.lang.Object'i örnekler → "Comparator değil" hatası.
        // Bu, testlerin anlamlı olduğunu ve açığın gerçek olduğunu kanıtlar.
        var p = new Processor(false);
        p.setConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, java.lang.Boolean.FALSE);
        var compiler = p.newXsltCompiler();
        compiler.setErrorList(new java.util.ArrayList());
        var exe = compiler.compile(new javax.xml.transform.stream.StreamSource(
            new java.io.StringReader(Stylesheet($"<xsl:value-of select=\"compare('a','b', {DynObject})\"/>")), "urn:x"));
        var t = exe.load30();
        t.setErrorReporter(new SilentReporter());
        var doc = p.newDocumentBuilder().build(new javax.xml.transform.stream.StreamSource(new java.io.StringReader("<r/>"), "urn:i"));

        var ex = Assert.ThrowsAny<Exception>(() => t.applyTemplates(doc, t.newSerializer(new java.io.StringWriter())));

        output.WriteLine(ex.Message);
        Assert.Contains("Comparator", ex.Message);
    }

    [Fact]
    public void CollationClassLiteral_Rejected()
    {
        var d = Fail(Stylesheet($"<xsl:value-of select=\"compare('a','b', '{ObjectClassCollation}')\"/>"));
        Assert.Equal(XsltFailureKind.Security, d.Kind);
        Assert.Equal(SandboxCodes.CollationDenied, d.ErrorCode);
    }

    [Fact]
    public void SaxonCollationClass_Rejected() => CollationClassLiteral_Rejected();

    [Fact]
    public void UcaWithPercent_Rejected()
    {
        var d = Fail(Stylesheet("<xsl:value-of select=\"compare('a','b','http://www.w3.org/2013/collation/UCA?lang=tr%2DTR')\"/>"));
        Assert.Equal(XsltFailureKind.Security, d.Kind);
        Assert.Equal(SandboxCodes.CollationDenied, d.ErrorCode);
    }

    [Fact]
    public void Collation_PercentEscape_CompileErrorNotCrash()
    {
        // IKVM0117 (AnyURIValue.decode → NoSuchMethodError) ham Throwable olarak kaçmamalı.
        var d = Fail(Stylesheet("<xsl:for-each select=\"('b','a')\"><xsl:sort select=\".\" collation=\"http://www.w3.org/2013/collation/UCA?lang=tr%2DTR\"/><xsl:value-of select=\".\"/></xsl:for-each>"));
        Assert.NotEqual(XsltFailureKind.EngineInternal, d.Kind);
        Assert.Equal(XsltFailureKind.Security, d.Kind);
    }

    [Fact]
    public void DefaultCollation_SaxonClass_NotLoaded()
    {
        // default-collation yalnız tanınan URI'leri seçer; sınıflı URI tanınmaz → statik hata (XTSE0125) ya da
        // sandbox reddi. Her iki durumda da başarı/sınıf örneği yok.
        var xslt = $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}" default-collation="{ObjectClassCollation}">
              <xsl:output omit-xml-declaration="yes"/>
              <xsl:template match="/"><out><xsl:value-of select="compare('a','b')"/></out></xsl:template>
            </xsl:stylesheet>
            """;
        var d = Fail(xslt);
        output.WriteLine($"{d.Kind}/{d.ErrorCode}: {d.Message}");
        Assert.True(d.Kind is XsltFailureKind.Security or XsltFailureKind.Compile, d.Kind.ToString());
        Assert.True(d.ErrorCode is SandboxCodes.CollationDenied or "XTSE0125", d.ErrorCode);
    }

    [Fact]
    public void Codepoint_Allowed()
    {
        Assert.Equal("<out>-1</out>", Render(Stylesheet("<xsl:value-of select=\"compare('a','b','http://www.w3.org/2005/xpath-functions/collation/codepoint')\"/>")));
        Assert.Equal("<out>ab</out>", Render(Stylesheet("<xsl:for-each select=\"('b','a')\"><xsl:sort select=\".\" collation=\"http://www.w3.org/2005/xpath-functions/collation/codepoint\"/><xsl:value-of select=\".\"/></xsl:for-each>")));
        Assert.Equal("<out>0</out>", Render(Stylesheet("<xsl:value-of select=\"compare('A','a','http://www.w3.org/2005/xpath-functions/collation/html-ascii-case-insensitive')\"/>")));
    }

    [Fact]
    public void UcaPlain_Allowed()
    {
        // Türkçe UCA: 'ç' 'c'den sonra, 'd'den önce sıralanır (codepoint'te en sona düşer).
        var xslt = Stylesheet("<xsl:for-each select=\"('d','ç','c')\"><xsl:sort select=\".\" collation=\"http://www.w3.org/2013/collation/UCA?lang=tr\"/><xsl:value-of select=\".\"/></xsl:for-each>");
        Assert.Equal("<out>cçd</out>", Render(xslt));
        Assert.Equal("<out>cçd</out>", Render(Stylesheet("<xsl:for-each select=\"('d','ç','c')\"><xsl:sort select=\".\" lang=\"tr\"/><xsl:value-of select=\".\"/></xsl:for-each>")));
        Assert.Equal("<out>-1</out>", Render(Stylesheet("<xsl:value-of select=\"compare('a','b', concat('http://www.w3.org/2013/collation/UCA', '?lang=tr;strength=primary'))\"/>")));
    }

    [Theory]
    [InlineData(RestrictedCollationResolver.CodepointUri, true)]
    [InlineData(RestrictedCollationResolver.HtmlAsciiCaseInsensitiveUri, true)]
    [InlineData("http://www.w3.org/2013/collation/UCA", true)]
    [InlineData("http://www.w3.org/2013/collation/UCA?lang=tr", true)]
    [InlineData("http://www.w3.org/2013/collation/UCA?lang=tr;strength=primary;fallback=yes", true)]
    [InlineData("http://www.w3.org/2013/collation/UCA?lang=tr%2DTR", false)]
    [InlineData("http://www.w3.org/2013/collation/UCA?class=x", false)]
    [InlineData("http://www.w3.org/2013/collation/UCA?CLASS=x", false)]
    [InlineData("http://www.w3.org/2013/collation/UCAx", false)]
    [InlineData("http://www.w3.org/2013/collation/UCA?lang=tr&x=y", false)]
    [InlineData("http://saxon.sf.net/collation?class=java.lang.Object", false)]
    [InlineData("http://saxon.sf.net/collation", false)]
    [InlineData("HTTP://www.w3.org/2005/xpath-functions/collation/codepoint", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowed_Matrix(string? uri, bool allowed) => Assert.Equal(allowed, RestrictedCollationResolver.IsAllowed(uri));

    private sealed class RecordingCollationResolver : CollationURIResolver
    {
        public List<string> Calls { get; } = [];

        public StringCollator resolve(string collationURI, Configuration config)
        {
            Calls.Add(collationURI);
            return null!;
        }
    }

    private sealed class SilentReporter : ErrorReporter
    {
        public void report(XmlProcessingError e)
        {
        }
    }
}
