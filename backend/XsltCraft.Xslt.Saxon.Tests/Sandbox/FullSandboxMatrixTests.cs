using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests.Sandbox;

/// <summary>
/// K3 tam matris (SR6, G0 O-2/O-3): üretim motoru (<see cref="SaxonEngineOptions.Isolated"/> ve InProcessGrid), XsltSafety
/// taraması OLMADAN. Her vektör için: (1) beklenen BELİRLİ kind + kod ya da birebir çıktı, (2) secret işaretçisi çıktıda /
/// mesajda yok, (3) iki loopback dinleyiciye (sessiz + gerçek HTTP yanıtı dönen) bağlantı yok, (4) result-document dosyası
/// yok. Hangi katmanın engellediği <see cref="K3Vector.Layer"/>'da; katmanın TEK BAŞINA etkisi <see cref="FullMinusLayerTests"/>'te.
/// </summary>
[Collection(nameof(SecretFixtureCollection))]
public sealed class FullSandboxMatrixTests(SecretFixture fx, ITestOutputHelper output)
{
    public static TheoryData<string> Vectors => new(K3Vectors.All.Select(v => v.Name));

    [Theory]
    [MemberData(nameof(Vectors))]
    public void FullSandbox_BlocksVector(string name) => AssertBlocked(K3Vectors.Get(name), Engine);

    [Theory]
    [MemberData(nameof(Vectors))]
    public void FullSandbox_BlocksVector_GridProfile(string name) => AssertBlocked(K3Vectors.Get(name), GridEngine);

    [Fact]
    public async Task LocalTcpListener_ZeroConnections()
    {
        // Kontrol: dinleyiciler gerçekten çalışıyor (canlı olan HTTP 200 + işaretçi döner).
        var before = fx.LiveConnections;
        Assert.Contains(SecretFixture.Marker, await fx.FetchLiveAsync("control.xml"));
        Assert.Equal(before + 1, fx.LiveConnections);

        var silent = fx.Connections;
        var live = fx.LiveConnections;
        foreach (var v in K3Vectors.All)
            _ = TryRender(v.Xslt(fx), v.Xml?.Invoke(fx) ?? "<r/>", Engine);

        Assert.Equal(silent, fx.Connections);
        Assert.Equal(live, fx.LiveConnections);
    }

    [Fact]
    public void Transform_InnerStylesheet_StillSandboxed()
    {
        var baseline = fx.TotalConnections;
        // fn:transform'u kapatan Saxon özelliği yok (G0 Y-1): iç stylesheet ÇALIŞIR — ama dış sandbox'ı devralır.
        Assert.Equal("<out><r>ICERDE</r></out>", Body(LayeredResult.Ran(Render(K3Vectors.Transform("ICERDE")))));

        var innerDoc = Fail(K3Vectors.Transform($"<xsl:copy-of select=\"doc('{SecretFixture.FileUri(fx.SecretXml)}')\"/>"));
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.ResourceDenied), (innerDoc.Kind, innerDoc.ErrorCode));

        var innerText = Fail(K3Vectors.Transform($"<xsl:value-of select=\"unparsed-text('{SecretFixture.FileUri(fx.SecretTxt)}')\"/>"));
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.UnparsedTextDenied), (innerText.Kind, innerText.ErrorCode));

        var innerHttp = Fail(K3Vectors.Transform($"<xsl:copy-of select=\"doc('{fx.LiveHttpUri("inner.xml")}')\"/>"));
        Assert.Equal(SandboxCodes.ResourceDenied, innerHttp.ErrorCode);

        var innerInclude = Fail(K3Vectors.Transform("x", innerTop: $"<xsl:include href=\"{SecretFixture.FileUri(fx.SecretXsl)}\"/>"));
        Assert.Equal(SandboxCodes.ResourceDenied, innerInclude.ErrorCode);

        // İç ortam / sistem özellikleri / result-document da dış yapılandırmadan.
        Assert.Equal("<out><r>[|0|]</r></out>", Body(TryRender(K3Vectors.Transform(
            $"[<xsl:value-of select=\"environment-variable('{SecretFixture.EnvName}')\"/>|<xsl:value-of select=\"count(available-environment-variables())\"/>|<xsl:value-of select=\"system-property('user.dir')\"/>]"), "<r/>", Engine)));

        var innerResultDoc = Fail(K3Vectors.Transform("<xsl:result-document href=\"x.xml\"><y/></xsl:result-document>"));
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.SaxonPolicy), (innerResultDoc.Kind, innerResultDoc.ErrorCode));

        // İç stylesheet'in kendi sürümü 3.0 olsa da XPath 2.0 tavanı devralınır.
        var inner30 = Fail(Stylesheet($"<xsl:copy-of select=\"map:get(transform(map:merge((map:entry('stylesheet-text', '{InnerV3("let $x := 1 return $x")}'), map:entry('source-node', /)))), 'output')\"/>", extraNs: K3Vectors.MapNs));
        Assert.Equal((XsltFailureKind.Unsupported, "XPST0003"), (inner30.Kind, inner30.ErrorCode));

        Assert.Equal(baseline, fx.TotalConnections);
    }

    [Fact]
    public void XslEvaluate_RejectedByXPath20Layer_AtCompile()
    {
        // Üretimde xsl:evaluate DERLEMEDE XPath 2.0 tavanına takılır (XPST0003 → Unsupported); DISABLE_XSL_EVALUATE'e hiç gelinmez.
        var d = Fail(K3Vectors.Get("xsl:evaluate").Xslt(fx));
        Assert.Equal((XsltFailureKind.Unsupported, "XPST0003"), (d.Kind, d.ErrorCode));

        // Aynı sonucu yalnız XPath20 katmanı (diğer her şey kapalı) da verir.
        var only = LayeredSandbox.Create(SandboxLayer.XPath20).Run(K3Vectors.Get("xsl:evaluate").Xslt(fx));
        Assert.Equal((XsltFailureKind.Unsupported, "XPST0003"), (only.Diagnostic?.Kind, only.Diagnostic?.ErrorCode));
    }

    [Fact]
    public void XslEvaluate_RejectedByDisableXslEvaluateLayer_WhenXPath20Absent()
    {
        // XPath20 yokken ikinci (bağımsız) katman: DISABLE_XSL_EVALUATE → ÇALIŞMA ANINDA XTDE3175 ("not available in this
        // configuration") → Saxon politika reddi (Security, SandboxSaxonPolicy).
        var xslt = Stylesheet("<xsl:evaluate xpath=\"'1+1'\"/>", version: "3.0");
        var r = LayeredSandbox.Create(SandboxLayer.Full & ~SandboxLayer.XPath20).Run(xslt);
        output.WriteLine(r.ToString());
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.SaxonPolicy, "runtime"), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode, r.Phase));

        // Kontrol: iki katman da yoksa xsl:evaluate gerçekten çalışır (katmanlar boşuna değil).
        var open = LayeredSandbox.Create(SandboxLayer.Full & ~SandboxLayer.XPath20 & ~SandboxLayer.Dynamic).Run(xslt);
        Assert.Equal("<out>2</out>", Body(open));
    }

    private const string RecursiveStatic = "let $f := function($g, $n) { 1 + $g($g, $n + 1) } return $f($f, 0) gt 0";

    public static TheoryData<string, bool> StaticExpressionVectors => new()
    {
        { "use-when", false }, { "use-when", true },
        { "lre-xsl-use-when", false }, { "lre-xsl-use-when", true },
        { "shadow-use-when", false }, { "shadow-use-when", true },
    };

    [Theory]
    [MemberData(nameof(StaticExpressionVectors))]
    public void StaticExpression_RejectedBeforeSaxon_OnSmallStack(string variant, bool grid)
    {
        // G2 security #1: use-when / gölge _use-when XPath 3.1 ile derlenir (XPATH_VERSION_FOR_XSLT=20 tavanı dışı);
        // özyinelemeli sürüm 16 MB'ta (Api süreç içi profil) derlemede yığını taşırıp süreci öldürürdü. Artık .NET ön
        // okumasında motor düzeyinde Security (XsltSafety'den bağımsız). 16 MB thread: süreç ölmez.
        var body = variant switch
        {
            "use-when" => $"<xsl:template match=\"/\" use-when=\"{RecursiveStatic}\"><o/></xsl:template>",
            "lre-xsl-use-when" => $"<xsl:template name=\"t\"><o xsl:use-when=\"{RecursiveStatic}\"/></xsl:template>",
            _ => "<xsl:template match=\"/\" _use-when=\"{concat('let $f := func', 'tion($g, $n) ', codepoints-to-string(123), ' 1 + $g($g, $n + 1) ', codepoints-to-string(125), ' return $f($f, 0) gt 0')}\"><o/></xsl:template>",
        };
        var xslt = Stylesheet("<x/>", top: body);

        var d = OnBigStack(() => Fail(xslt, engine: grid ? GridEngine : Engine), stackMb: 16);

        Assert.Equal((XsltFailureKind.Security, SandboxCodes.StylesheetStaticExpression), (d.Kind, d.ErrorCode));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("false")]
    [InlineData("0")]
    public void StaticNo_Allowed(string value)
    {
        var xslt = Stylesheet("<xsl:value-of select=\"$v\"/>", top: $"<xsl:variable name=\"v\" static=\"{value}\" select=\"'ok'\"/>", version: "3.0");
        Assert.Equal("<out>ok</out>", Render(xslt));
    }

    [Fact]
    public void XslMessage_And_Trace_ProduceNoOutputLeak()
    {
        // stderr boşluğu SaxonStdioTests.XslMessage_And_Trace_ProduceNoStderr'de; burada içerik çıktıya/teşhise sızmaz.
        var d = Fail(Stylesheet($"<xsl:message terminate=\"yes\"><xsl:value-of select=\"environment-variable('{SecretFixture.EnvName}')\"/>MSG</xsl:message>"));
        Assert.DoesNotContain(SecretFixture.Marker, d.Message);
    }

    [Fact]
    public void LayeredSandbox_Full_MatchesProductionFactory()
    {
        // Test seam'i üretimden sapmamalı: aynı Feature değerleri + aynı çözücü türleri.
        var prod = new SaxonProcessorFactory(SaxonEngineOptions.Isolated).CreateProcessor();
        var test = LayeredSandbox.Create(SandboxLayer.Full).Processor;
        foreach (var feature in new net.sf.saxon.lib.Feature[]
        {
            net.sf.saxon.lib.Feature.ALLOWED_PROTOCOLS, net.sf.saxon.lib.Feature.DISABLE_XSL_EVALUATE,
            net.sf.saxon.lib.Feature.ALLOW_EXTERNAL_FUNCTIONS, net.sf.saxon.lib.Feature.ALLOW_SYNTAX_EXTENSIONS,
            net.sf.saxon.lib.Feature.XINCLUDE, net.sf.saxon.lib.Feature.DTD_VALIDATION,
            net.sf.saxon.lib.Feature.XPATH_VERSION_FOR_XSLT, net.sf.saxon.lib.Feature.REGEX_BACKTRACKING_LIMIT,
        })
        {
            Assert.Equal(prod.getConfigurationProperty(feature)?.ToString(), test.getConfigurationProperty(feature)?.ToString());
        }

        var pc = prod.getUnderlyingConfiguration();
        var tc = test.getUnderlyingConfiguration();
        Assert.Equal(pc.getResourceResolver().GetType(), tc.getResourceResolver().GetType());
        Assert.Equal(pc.getUnparsedTextURIResolver().GetType(), tc.getUnparsedTextURIResolver().GetType());
        Assert.Equal(pc.getCollectionFinder().GetType(), tc.getCollectionFinder().GetType());
        Assert.Equal(pc.getCollationURIResolver().GetType(), tc.getCollationURIResolver().GetType());
        Assert.Equal(prod.getConfigurationProperty(net.sf.saxon.lib.Feature.ENVIRONMENT_VARIABLE_RESOLVER).GetType(),
            test.getConfigurationProperty(net.sf.saxon.lib.Feature.ENVIRONMENT_VARIABLE_RESOLVER).GetType());
        Assert.Equal(pc.getParseOptions().getParserFeatures().ToString(), tc.getParseOptions().getParserFeatures().ToString());

        // Davranışsal eşdeğerlik: tüm vektörlerde aynı kind/kod.
        // Statik ifade reddi Saxon öncesi .NET ön okumasıdır (seam'de kapalı — Saxon katmanları tek tek ölçülür).
        foreach (var v in K3Vectors.All.Where(v => v.Code != SandboxCodes.StylesheetStaticExpression))
        {
            var xml = v.Xml?.Invoke(fx) ?? "<r/>";
            var p = TryRender(v.Xslt(fx), xml, Engine);
            var t = OnBigStack(() => LayeredSandbox.Create(SandboxLayer.Full).Run(v.Xslt(fx), xml));
            Assert.True((p.Diagnostic?.Kind, p.Diagnostic?.ErrorCode) == (t.Diagnostic?.Kind, t.Diagnostic?.ErrorCode), $"{v.Name}: üretim={p} seam={t}");
        }
    }

    private void AssertBlocked(K3Vector v, SaxonXsltEngine engine)
    {
        var xslt = v.Xslt(fx);
        var xml = v.Xml?.Invoke(fx) ?? "<r/>";
        var connections = fx.TotalConnections;

        var r = TryRender(xslt, xml, engine);
        output.WriteLine($"{v.Name} [{v.Layer}]: {r}");

        Assert.False(r.Leaked(SecretFixture.Marker), r.ToString());
        Assert.Equal(connections, fx.TotalConnections);
        Assert.False(File.Exists(fx.ResultDocPath));
        Assert.False(File.Exists(Path.Combine(Environment.CurrentDirectory, "out.xml")));

        if (v.Kind is { } kind)
        {
            Assert.NotNull(r.Diagnostic);
            Assert.Equal(kind, r.Diagnostic!.Kind);
            Assert.Equal(v.Code, r.Diagnostic.ErrorCode);
            Assert.Equal(XsltEngineKind.Saxon, r.Diagnostic.Engine);
        }
        else if (v.Output == K3Vectors.XiLiteral)
        {
            // XINCLUDE=false: eleman literal kopyalanır, href okunmaz.
            Assert.Null(r.Diagnostic);
            Assert.Contains("<xi:include", r.Output, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(r.Diagnostic);
            Assert.Equal(v.Output, Body(r));
        }
    }

    internal static LayeredResult TryRender(string xslt, string xml, SaxonXsltEngine engine)
    {
        try
        {
            return LayeredResult.Ran(OnBigStack(() => Render(xslt, xml, engine: engine)));
        }
        catch (XsltProcessingException ex)
        {
            return LayeredResult.Failed(ex.Diagnostic, "engine");
        }
    }

    /// <summary>XML bildirimi ve ad alanı bildirimleri ayıklanmış çıktı (karşılaştırma için).</summary>
    internal static string Body(LayeredResult r)
    {
        Assert.True(r.Succeeded, r.ToString());
        var s = r.Output!;
        if (s.StartsWith("<?xml", StringComparison.Ordinal))
            s = s[(s.IndexOf("?>", StringComparison.Ordinal) + 2)..];
        return System.Text.RegularExpressions.Regex.Replace(s, " xmlns:\\w+=\"[^\"]*\"", "");
    }

    private static string InnerV3(string expr) =>
        System.Security.SecurityElement.Escape($"<xsl:stylesheet version=\"3.0\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"><r><xsl:value-of select=\"{expr}\"/></r></xsl:template></xsl:stylesheet>")
            .Replace("&apos;", "''", StringComparison.Ordinal);
}
