using System.Diagnostics;
using net.sf.saxon.lib;
using net.sf.saxon.s9api;
using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Input;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Sandbox;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>Sandbox yapılandırması ve tek katman kanıtları (SR1, SR3, SR4, SR5; ADR 008 §6).</summary>
[Collection(nameof(SecretFixtureCollection))]
public sealed class SaxonSandboxTests(SecretFixture fx, ITestOutputHelper output)
{
    // ---- yapılandırma ------------------------------------------------------------------------------

    [Fact]
    public void Configuration_AsDeclared()
    {
        var processor = new SaxonProcessorFactory(SaxonEngineOptions.Isolated).CreateProcessor();
        var config = processor.getUnderlyingConfiguration();

        Assert.Equal(SaxonProcessorFactory.AllowedProtocolsValue, processor.getConfigurationProperty(Feature.ALLOWED_PROTOCOLS));
        Assert.Equal(java.lang.Boolean.TRUE, processor.getConfigurationProperty(Feature.DISABLE_XSL_EVALUATE));
        Assert.Equal(java.lang.Boolean.FALSE, processor.getConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS));
        Assert.Equal(java.lang.Boolean.FALSE, processor.getConfigurationProperty(Feature.ALLOW_SYNTAX_EXTENSIONS));
        Assert.Equal(java.lang.Boolean.FALSE, processor.getConfigurationProperty(Feature.XINCLUDE));
        Assert.Equal(20, ((java.lang.Integer)processor.getConfigurationProperty(Feature.XPATH_VERSION_FOR_XSLT)).intValue());
        Assert.Equal(100_000, ((java.lang.Integer)processor.getConfigurationProperty(Feature.REGEX_BACKTRACKING_LIMIT)).intValue());
        Assert.IsType<EmptyEnvironmentVariableResolver>(processor.getConfigurationProperty(Feature.ENVIRONMENT_VARIABLE_RESOLVER));
        Assert.IsType<RestrictedCollationResolver>(config.getCollationURIResolver());
        Assert.IsType<DenyingResourceResolver>(config.getResourceResolver());
        Assert.IsType<DenyingUnparsedTextResolver>(config.getUnparsedTextURIResolver());
        Assert.IsType<DenyingCollectionFinder>(config.getCollectionFinder());
        Assert.IsType<SilentLogger>(config.getLogger());
        Assert.True(config.getParseOptions().getParserFeatures().containsKey(SaxonProcessorFactory.DisallowDoctypeFeature));
        Assert.False(processor.isSchemaAware());
    }

    [Fact]
    public void AllowedProtocols_Value_BlocksAllSchemes()
    {
        // Yalnız ALLOWED_PROTOCOLS katmanı (resolver yok) — iki aday değer de tüm şemaları engeller; "" = "all" tuzağı.
        var vectors = new[]
        {
            $"doc('{SecretFixture.FileUri(fx.SecretXml)}')",
            $"doc('{fx.HttpUri("p.xml")}')",
            $"unparsed-text('{SecretFixture.FileUri(fx.SecretTxt)}')",
            "doc('data:application/xml,%3Cd%3EDATA_OK%3C/d%3E')",
            "unparsed-text('classpath:META-INF/MANIFEST.MF')",
            $"doc('jar:{SecretFixture.FileUri(typeof(Processor).Assembly.Location)}!/x.xml')",
        };

        foreach (var value in new[] { SaxonProcessorFactory.AllowedProtocolsValue, "-" })
        {
            var p = new Processor(false);
            p.setConfigurationProperty(Feature.ALLOWED_PROTOCOLS, value);
            Assert.Equal(value, p.getConfigurationProperty(Feature.ALLOWED_PROTOCOLS));
            foreach (var v in vectors)
            {
                var r = RawRun(p, Stylesheet($"<xsl:copy-of select=\"{v}\"/>"));
                output.WriteLine($"'{value}' {v}: {r}");
                Assert.Contains("has been prohibited", r);
                Assert.DoesNotContain(SecretFixture.Marker, r);
            }
        }

        var trap = new Processor(false);
        trap.setConfigurationProperty(Feature.ALLOWED_PROTOCOLS, "");
        Assert.Equal("all", trap.getConfigurationProperty(Feature.ALLOWED_PROTOCOLS));
        Assert.Contains(SecretFixture.Marker, RawRun(trap, Stylesheet($"<xsl:copy-of select=\"{vectors[0]}\"/>")));
        Assert.Equal(0, fx.Connections);
    }

    [Fact]
    public void SystemProperty_JavaProps_BlockedByAefLayerAlone()
    {
        var xslt = Stylesheet("[<xsl:value-of select=\"system-property('user.dir'), system-property('java.home'), system-property('user.name')\" separator=\"\"/>]");

        var open = new Processor(false);
        open.setConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, java.lang.Boolean.TRUE);
        Assert.NotEqual("RAN:<out>[]</out>", RawRun(open, xslt)); // kontrol: varsayılan sızdırır

        var aefOnly = new Processor(false);
        aefOnly.setConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, java.lang.Boolean.FALSE);
        Assert.Equal("RAN:<out>[]</out>", RawRun(aefOnly, xslt));

        Assert.Equal("<out>[]</out>", Render(xslt));
        Assert.Equal("<out>Saxonica</out>", Render(Stylesheet("<xsl:value-of select=\"system-property('xsl:vendor')\"/>")));
    }

    [Fact]
    public void SecureProcessing_Applied()
    {
        // JAXP FEATURE_SECURE_PROCESSING uygulanabiliyor; Saxon 12.10'da TEK etkisi ALLOW_EXTERNAL_FUNCTIONS=false.
        // Protokol / ayrıştırıcı / resolver'lara dokunmaz → sandbox iddiası olarak kullanılamaz (ADR notu).
        var p = new Processor(false);
        Assert.Equal(java.lang.Boolean.TRUE, p.getConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS));
        SecureProcessing.Apply(p.getUnderlyingConfiguration());
        Assert.Equal(java.lang.Boolean.FALSE, p.getConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS));
        Assert.Equal("all", p.getConfigurationProperty(Feature.ALLOWED_PROTOCOLS));
        Assert.Contains(SecretFixture.Marker, RawRun(p, Stylesheet($"<xsl:copy-of select=\"doc('{SecretFixture.FileUri(fx.SecretXml)}')\"/>")));
    }

    // ---- süreç içi (Api) profili: env secret'lar süreçte VAR ---------------------------------------

    [Fact]
    public void EnvVar_Empty_InProcess()
    {
        Assert.Equal(SecretFixture.Marker, Environment.GetEnvironmentVariable(SecretFixture.EnvName));
        var html = Render(Stylesheet($"[<xsl:value-of select=\"environment-variable('{SecretFixture.EnvName}')\"/>|<xsl:value-of select=\"count(available-environment-variables())\"/>]"), engine: GridEngine);
        Assert.Equal("<out>[|0]</out>", html);
    }

    [Fact]
    public void SystemProperty_Blocked_InProcess()
    {
        var html = Render(Stylesheet("[<xsl:value-of select=\"system-property('user.dir'), system-property('java.home'), system-property('java.class.path'), system-property('user.home')\" separator=\"\"/>]"), engine: GridEngine);
        Assert.Equal("<out>[]</out>", html);
    }

    // ---- stylesheet DTD (G0 Y-1) --------------------------------------------------------------------

    [Fact]
    public void Stylesheet_WithInternalDtd_RejectedBeforeSaxon()
    {
        var xslt = $"""<!DOCTYPE xsl:stylesheet [ <!ENTITY e "{SecretFixture.Marker}"> ]><xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><o>&e;</o></xsl:template></xsl:stylesheet>""";
        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(xslt, XsltExecutionLimits.Default)).Diagnostic;

        // Kod .NET ön okumasından gelir (Saxon'a hiç ulaşmadı).
        Assert.Equal(XsltFailureKind.Security, d.Kind);
        Assert.Equal(SandboxCodes.StylesheetDtd, d.ErrorCode);
        Assert.DoesNotContain(SecretFixture.Marker, d.Message);
    }

    [Fact]
    public void Stylesheet_BillionLaughs_Rejected()
    {
        var ents = string.Concat(Enumerable.Range(1, 9).Select(i => $"<!ENTITY l{i} \"{string.Concat(Enumerable.Repeat($"&l{i - 1};", 10))}\">"));
        var xslt = $"""<!DOCTYPE xsl:stylesheet [ <!ENTITY l0 "lol">{ents} ]><xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><o>&l9;</o></xsl:template></xsl:stylesheet>""";
        var sw = Stopwatch.StartNew();

        var d = Assert.Throws<XsltProcessingException>(() => Engine.Compile(xslt, XsltExecutionLimits.Default)).Diagnostic;

        Assert.Equal(SandboxCodes.StylesheetDtd, d.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString());
    }

    [Fact]
    public void Stylesheet_Dtd_SecondLayer_SaxParserRejects()
    {
        // Ön okuma ATLANDIĞINDA da Saxon'a verilen JDK SAX okuyucusu DOCTYPE'ı reddeder.
        var xslt = $"""<!DOCTYPE xsl:stylesheet [ <!ENTITY e "{SecretFixture.Marker}"> ]><xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}"><xsl:template match="/"><o>&e;</o></xsl:template></xsl:stylesheet>""";
        var factory = new SaxonProcessorFactory(SaxonEngineOptions.Isolated);
        var reporter = new BoundedErrorReporter(4, 512);
        var compiler = factory.CreateCompiler(factory.CreateProcessor(), reporter);

        var ex = Assert.ThrowsAny<Exception>(() => compiler.compile(StylesheetSource.Create(xslt)));
        var all = ex.Message + string.Join("|", reporter.Errors.Select(e => e.Message));
        output.WriteLine(all);
        Assert.Contains("DOCTYPE", all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseXml_InternalEntity_RejectedBySaxonParserLayer()
    {
        var xslt = Stylesheet("<xsl:value-of select=\"string-length(string(parse-xml('&lt;!DOCTYPE x [&lt;!ENTITY a &quot;aaaaaaaaaa&quot;&gt;]&gt;&lt;x&gt;&amp;a;&lt;/x&gt;')))\"/>");
        var d = Fail(xslt);
        output.WriteLine($"{d.Kind}/{d.ErrorCode}: {d.Message}");
        Assert.NotEqual(XsltFailureKind.EngineInternal, d.Kind);
    }

    // ---- çıktı kanalları (SR3) ----------------------------------------------------------------------

    [Fact]
    public void MessageHandler_IsBounded()
    {
        var xslt = Stylesheet("<xsl:for-each select=\"1 to 1000\"><xsl:message><xsl:value-of select=\"string-join(for $i in 1 to 500 return 'm', '')\"/></xsl:message></xsl:for-each>done");
        var compiled = (SaxonCompiledXslt)Engine.Compile(xslt, XsltExecutionLimits.Default);
        SaxonTransformProbe? probe = null;
        var sw = new StringWriter();

        compiled.Transform("<r/>", sw, XsltExecutionLimits.Default, p => probe = p);

        Assert.Equal("<out>done</out>", sw.ToString());
        Assert.Equal(1000, probe!.MessageCount);
        Assert.Equal(Engine.Options.MaxRetainedMessages, probe.RetainedMessages.Count);
        Assert.All(probe.RetainedMessages, m => Assert.True(m.Length <= Engine.Options.MaxMessageChars + 1));
    }

    [Fact]
    public void XslMessageTerminate_IsRuntimeError()
    {
        var d = Fail(Stylesheet("<xsl:message terminate=\"yes\">dur</xsl:message>"));
        Assert.Equal(XsltFailureKind.Runtime, d.Kind);
    }

    // ---- ReDoS (SR4) --------------------------------------------------------------------------------

    [Fact]
    public void Redos_FailsWithRegexLimit()
    {
        // Saxon 12.10 Feature.REGEX_BACKTRACKING_LIMIT mevcut (varsayılan 10.000.000 ≈ 4 sn/çağrı); bizde 100.000.
        var sw = Stopwatch.StartNew();
        var d = Fail(Stylesheet("<xsl:value-of select=\"matches(concat(string-join(for $i in 1 to 28 return 'a', ''), '!'), '^(a+)+$')\"/>"));
        sw.Stop();

        Assert.Equal(XsltFailureKind.LimitExceeded, d.Kind);
        Assert.Equal(LimitCodes.Regex, d.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString());
    }

    [Fact]
    public void Regex_Legitimate_StillWorks()
    {
        var xslt = Stylesheet("<xsl:value-of select=\"matches('2026-10-24', '^\\d{4}-\\d{2}-\\d{2}$'), replace('TR12 0006 1005', '\\s', ''), count(tokenize(string-join(for $i in 1 to 5000 return 'a,b', ','), ','))\"/>");
        Assert.Equal("<out>true TR1200061005 10000</out>", Render(xslt));
    }

    private static string RawRun(Processor p, string xslt)
    {
        try
        {
            var compiler = p.newXsltCompiler();
            compiler.setErrorList(new java.util.ArrayList());
            var exe = compiler.compile(new javax.xml.transform.stream.StreamSource(new java.io.StringReader(xslt), "urn:x"));
            var t = exe.load30();
            t.setErrorReporter(new SilentReporter());
            var doc = p.newDocumentBuilder().build(new javax.xml.transform.stream.StreamSource(new java.io.StringReader("<r/>"), "urn:i"));
            var sw = new java.io.StringWriter();
            t.setGlobalContextItem(doc);
            t.applyTemplates(doc, t.newSerializer(sw));
            return "RAN:" + sw.toString();
        }
        catch (Exception ex)
        {
            return "ERR:" + ex.Message;
        }
    }

    private sealed class SilentReporter : ErrorReporter
    {
        public void report(XmlProcessingError e)
        {
        }
    }
}
