using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Sandbox.FullSandboxMatrixTests;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests.Sandbox;

/// <summary>
/// "Full eksi X" (ADR 008 §6, G0 O-2): her katman TEK BAŞINA çıkarılır; kalan katmanların neyi hâlâ engellediği ve
/// çıkarılan katmanın TEK koruma olduğu vektörlerin gerçekten SIZDIĞI kanıtlanır. Kontrol: sandbox'sız Processor'da
/// aynı vektörler sızar (yalnız testte). Varyantlar <see cref="LayeredSandbox"/> test seam'inden (üretim API'si aynı).
/// </summary>
[Collection(nameof(SecretFixtureCollection))]
public sealed class FullMinusLayerTests(SecretFixture fx, ITestOutputHelper output)
{
    private const string Marker = SecretFixture.Marker;

    // ---- kontrol: sandbox yok ------------------------------------------------------------------

    [Theory]
    [InlineData("doc(file)")]
    [InlineData("document(file)")]
    [InlineData("doc(http live)")]
    [InlineData("unparsed-text(file)")]
    [InlineData("unparsed-text(http live)")]
    [InlineData("unparsed-text-lines(file)")]
    [InlineData("json-doc(file)")]
    [InlineData("collection(dir)")]
    [InlineData("xsl:source-document")]
    [InlineData("xsl:merge for-each-source")]
    [InlineData("transform(stylesheet-location)")]
    [InlineData("transform(stylesheet-text) iç doc()")]
    [InlineData("transform(stylesheet-text) iç unparsed-text()")]
    [InlineData("parse-xml + harici entity")]
    [InlineData("stylesheet DTD (iç entity)")]
    [InlineData("stylesheet DTD (harici entity)")]
    [InlineData("environment-variable(secret)")]
    [InlineData("static değişken environment-variable")]
    [InlineData("static değişken unparsed-text() (derleme)")]
    [InlineData("doc(UNC file://127.0.0.1/C$/)")]
    [InlineData("doc(çıplak C:/)")]
    public void NoSandbox_Control_VectorLeaks(string name)
    {
        var r = Run(SandboxLayer.None, name);
        Assert.True(r.Leaked(Marker), $"Kontrol sızmadı (vektör/fikstür bozuk?): {r}");
    }

    [Fact]
    public void NoSandbox_Control_HttpReached_And_ResultDocumentWritten()
    {
        var live = fx.LiveConnections;
        Assert.True(Run(SandboxLayer.None, "doc(http live)").Leaked(Marker));
        Assert.True(fx.LiveConnections > live);

        var target = Path.Combine(fx.Dir, "control-result.xml");
        var r = LayeredSandbox.Create(SandboxLayer.None).Run(Stylesheet($"<xsl:result-document href=\"{SecretFixture.FileUri(target)}\"><x>{Marker}</x></xsl:result-document>"));
        Assert.True(r.Succeeded, r.ToString());
        Assert.Contains(Marker, File.ReadAllText(target));
        File.Delete(target);

        Assert.NotEqual("<out>[]</out>", Body(Run(SandboxLayer.None, "system-property(user.dir)")));
        Assert.Equal("<out>[1000]</out>", Body(Run(SandboxLayer.None, "parse-xml billion laughs")));
        Assert.Equal("<out>[true]</out>", Body(Run(SandboxLayer.None, "doc-available(file)")));
    }

    // ---- Resolvers -------------------------------------------------------------------------------

    [Theory]
    [InlineData("xsl:include(abs file)")]
    [InlineData("xsl:import(http)")]
    [InlineData("doc(file)")]
    [InlineData("doc(http live)")]
    [InlineData("doc(relative)")]
    [InlineData("doc(data:)")]
    [InlineData("doc(classpath:)")]
    [InlineData("doc(jar:)")]
    [InlineData("doc(UNC file://127.0.0.1/C$/)")]
    [InlineData("doc(çıplak C:/)")]
    [InlineData("doc(FILE: büyük harf)")]
    [InlineData("unparsed-text(file)")]
    [InlineData("unparsed-text(classpath:)")]
    [InlineData("json-doc(file)")]
    [InlineData("collection(dir)")]
    [InlineData("uri-collection(dir)")]
    [InlineData("xsl:source-document")]
    [InlineData("xsl:merge for-each-source")]
    [InlineData("transform(stylesheet-location)")]
    [InlineData("load-xquery-module")]
    [InlineData("use-when doc() (derleme)")]
    [InlineData("static değişken unparsed-text() (derleme)")]
    public void FullMinusResolvers_ProtocolsLayerAloneBlocks(string name)
    {
        // Resolver'lar yokken ALLOWED_PROTOCOLS tek başına tüm şemaları (data:, classpath:, jar: dahil) Saxon politikasıyla keser.
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.Resolvers, name);
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.SaxonPolicy), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
    }

    [Fact]
    public void FullMinusResolvers_AvailabilityFunctions_StayFalse()
    {
        foreach (var name in new[] { "doc-available(file)", "doc-available(http live)", "unparsed-text-available(file)" })
            Assert.Equal("<out>[false]</out>", Body(AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.Resolvers, name)));
    }

    [Fact]
    public void FullMinusResolversAndProtocols_UriVectorsLeak()
    {
        // İki URI katmanı birbirinin yedeği; İKİSİ birden çıkınca (diğer katmanlar açıkken) dosya + ağ sızar.
        var layers = SandboxLayer.Full & ~SandboxLayer.Resolvers & ~SandboxLayer.Protocols;
        foreach (var name in new[] { "doc(file)", "doc(http live)", "unparsed-text(file)", "json-doc(file)", "collection(dir)", "transform(stylesheet-location)" })
            Assert.True(Run(layers, name).Leaked(Marker), name);
    }

    // ---- Protocols -------------------------------------------------------------------------------

    [Theory]
    [InlineData("xsl:include(abs file)", SandboxCodes.ResourceDenied)]
    [InlineData("xsl:import(http)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(file)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(http live)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(data:)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(classpath:)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(jar:)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(UNC file://127.0.0.1/C$/)", SandboxCodes.ResourceDenied)]
    [InlineData("doc(çıplak C:/)", SandboxCodes.ResourceDenied)]
    [InlineData("unparsed-text(file)", SandboxCodes.UnparsedTextDenied)]
    [InlineData("unparsed-text(classpath:)", SandboxCodes.UnparsedTextDenied)]
    [InlineData("json-doc(file)", SandboxCodes.UnparsedTextDenied)]
    [InlineData("collection(dir)", SandboxCodes.CollectionDenied)]
    [InlineData("uri-collection(dir)", SandboxCodes.CollectionDenied)]
    [InlineData("transform(stylesheet-location)", SandboxCodes.ResourceDenied)]
    [InlineData("load-xquery-module", SandboxCodes.ResourceDenied)]
    [InlineData("use-when unparsed-text() (derleme)", SandboxCodes.UnparsedTextDenied)]
    public void FullMinusProtocols_ResolverLayerAloneBlocks(string name, string code)
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.Protocols, name);
        Assert.Equal((XsltFailureKind.Security, code), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
    }

    // ---- RuntimeIo (env + result-document işleyicisi) --------------------------------------------

    [Fact]
    public void FullMinusRuntimeIo_Environment_StillEmpty_AefIsSecondLayer()
    {
        // T2.3 ölçümü: boş EnvironmentVariableResolver yokken de env boş — ALLOW_EXTERNAL_FUNCTIONS=false (Dynamic)
        // environment-variable()/available-environment-variables()'ı da kapatıyor (bağımsız ikinci katman).
        var layers = SandboxLayer.Full & ~SandboxLayer.RuntimeIo;
        Assert.Equal("<out>[]</out>", Body(AssertNoLeak(layers, "environment-variable(secret)")));
        Assert.Equal("<out>[]</out>", Body(AssertNoLeak(layers, "static değişken environment-variable")));
        Assert.Equal("<out>[0]</out>", Body(AssertNoLeak(layers, "available-environment-variables()")));
    }

    [Fact]
    public void FullMinusDynamic_Environment_StillEmpty_ResolverIsFirstLayer()
    {
        var layers = SandboxLayer.Full & ~SandboxLayer.Dynamic;
        Assert.Equal("<out>[]</out>", Body(AssertNoLeak(layers, "environment-variable(secret)")));
        Assert.Equal("<out>[0]</out>", Body(AssertNoLeak(layers, "available-environment-variables()")));
    }

    [Fact]
    public void FullMinusRuntimeIoAndDynamic_EnvironmentLeaks()
    {
        // İki env katmanı birden çıkınca süreç ortamı (Api'de secret'lar!) çalışma ve derleme anında okunur.
        var layers = SandboxLayer.Full & ~SandboxLayer.RuntimeIo & ~SandboxLayer.Dynamic;
        Assert.True(Run(layers, "environment-variable(secret)").Leaked(Marker));
        Assert.True(Run(layers, "static değişken environment-variable").Leaked(Marker));
        Assert.NotEqual("<out>[0]</out>", Body(Run(layers, "available-environment-variables()")));
    }

    [Fact]
    public void FullMinusRuntimeIo_ResultDocument_StillBlockedAtCompileByAef()
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.RuntimeIo, "xsl:result-document(file)");
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.SaxonPolicy, "compile"), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode, r.Phase));
        Assert.False(File.Exists(fx.ResultDocPath));
    }

    // ---- Dynamic (AEF / evaluate / sözdizimi uzantıları) -----------------------------------------

    [Theory]
    [InlineData("system-property(user.dir)")]
    [InlineData("system-property(java.home)")]
    [InlineData("system-property(os.name)")]
    [InlineData("system-property(java.io.tmpdir)")]
    public void FullMinusDynamic_SystemPropertyLeaks_AefOnlyLayer(string name)
    {
        // SR5: system-property('java…') sızıntısını engelleyen TEK katman ALLOW_EXTERNAL_FUNCTIONS=false.
        var body = Body(Run(SandboxLayer.Full & ~SandboxLayer.Dynamic, name));
        Assert.NotEqual("<out>[]</out>", body);
    }

    [Fact]
    public void FullMinusDynamic_ResultDocument_RuntimeHandlerBlocks_NoFile()
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.Dynamic, "xsl:result-document(file)");
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.ResultDocumentDenied, "runtime"), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode, r.Phase));
        Assert.False(File.Exists(fx.ResultDocPath));
    }

    [Fact]
    public void FullMinusDynamic_ReflexiveCalls_StillUnavailableInHe()
    {
        // Saxon-HE'de yansımalı Java/.NET çağrısı hiç yok: AEF olmadan da XPST0017 (yalnız mesaj değişir).
        foreach (var name in new[] { "reflexive java: önekli", "reflexive clitype: önekli", "saxon:evaluate" })
        {
            var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.Dynamic, name);
            Assert.Equal((XsltFailureKind.Compile, "XPST0017"), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
        }
    }

    // ---- NoDtd -----------------------------------------------------------------------------------

    [Fact]
    public void FullMinusNoDtd_StylesheetInternalEntity_Leaks_OnlyLayer()
    {
        // G0 Y-1: stylesheet DTD'sini engelleyen tek katman ön okuma + SAX disallow-doctype (Saxon parse seçenekleri DEĞİL).
        Assert.True(Run(SandboxLayer.Full & ~SandboxLayer.NoDtd, "stylesheet DTD (iç entity)").Leaked(Marker));
    }

    [Fact]
    public void FullMinusNoDtd_StylesheetExternalEntity_ResolverBlocks()
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.NoDtd, "stylesheet DTD (harici entity)");
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.ResourceDenied), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
    }

    [Fact]
    public void FullMinusNoDtd_ParseXmlBillionLaughs_Expands_OnlyLayer()
    {
        // parse-xml iç entity genişlemesi (DoS) yalnız disallow-doctype-decl ile durur.
        Assert.Equal("<out>[1000]</out>", Body(Run(SandboxLayer.Full & ~SandboxLayer.NoDtd, "parse-xml billion laughs")));
    }

    [Fact]
    public void FullMinusNoDtd_ParseXmlExternalEntity_ResolverBlocks()
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.NoDtd, "parse-xml + harici entity");
        Assert.Equal((XsltFailureKind.Security, SandboxCodes.ResourceDenied), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
    }

    [Fact]
    public void FullMinusNoDtd_InputXmlDtd_StillRejectedByBridge()
    {
        // Girdi XML'i her zaman .NET köprüsünden (DtdProcessing.Prohibit) geçer — Saxon katmanından bağımsız.
        var r = AssertNoLeak(SandboxLayer.None, "girdi XML DTD/XXE");
        Assert.Equal(XsltFailureKind.Xml, r.Diagnostic?.Kind);
    }

    // ---- XPath20 ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("function-lookup(doc)", SandboxCodes.ResourceDenied)]
    [InlineData("doc#1 adlı referans", SandboxCodes.ResourceDenied)]
    [InlineData("Q{fn}doc EQName", SandboxCodes.ResourceDenied)]
    public void FullMinusXPath20_IndirectDoc_ResolverBlocks(string name, string code)
    {
        var r = AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.XPath20, name);
        Assert.Equal((XsltFailureKind.Security, code), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
    }

    [Fact]
    public void FullMinusXPath20_FunctionLookupEnv_StillEmpty()
    {
        Assert.Equal("<out>[]</out>", Body(AssertNoLeak(SandboxLayer.Full & ~SandboxLayer.XPath20, "function-lookup(environment-variable)")));
    }

    // ---- relative URI / CWD ----------------------------------------------------------------------

    [Fact]
    public void RelativeUri_CannotReachCwd()
    {
        // CWD'de appsettings benzeri secret var (fikstür). Worker CWD'si boş scratch dizindir; Api'de CWD = /app.
        Assert.Contains(Marker, File.ReadAllText(Path.Combine(Environment.CurrentDirectory, fx.CwdSecretName)));
        var vectors = new (string Name, string Xslt, string Code)[]
        {
            ("doc", Stylesheet($"<xsl:copy-of select=\"doc('{fx.CwdSecretXmlName}')\"/>"), SandboxCodes.ResourceDenied),
            ("document", Stylesheet($"<xsl:copy-of select=\"document('{fx.CwdSecretXmlName}')\"/>"), SandboxCodes.ResourceDenied),
            ("unparsed-text", Stylesheet($"<xsl:value-of select=\"unparsed-text('{fx.CwdSecretName}')\"/>"), SandboxCodes.UnparsedTextDenied),
            ("json-doc", Stylesheet($"<xsl:value-of select=\"map:get(map:get(json-doc('{fx.CwdSecretName}'), 'ConnectionStrings'), 'Db')\"/>", extraNs: K3Vectors.MapNs), SandboxCodes.UnparsedTextDenied),
            ("include", $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:include href=\"{fx.CwdSecretXslName}\"/><xsl:template match=\"/\"><out><xsl:call-template name=\"leak\"/></out></xsl:template></xsl:stylesheet>", SandboxCodes.ResourceDenied),
        };

        foreach (var (name, xslt, code) in vectors)
        {
            // 1) Üretim (urn: base) — tam sandbox.
            var prod = TryRender(xslt, "<r/>", Engine);
            output.WriteLine($"{name} üretim: {prod}");
            Assert.False(prod.Leaked(Marker), name);
            Assert.Equal((XsltFailureKind.Security, code), (prod.Diagnostic?.Kind, prod.Diagnostic?.ErrorCode));

            // 2) Stylesheet base URI'si CWD olsa bile (yanlış yapılandırma senaryosu) tam sandbox keser.
            var cwdBase = LayeredSandbox.Create(SandboxLayer.Full).Run(xslt, baseUri: SecretFixture.CwdFileUri);
            output.WriteLine($"{name} Full+CWD base: {cwdBase}");
            Assert.False(cwdBase.Leaked(Marker), name);
            Assert.Equal(XsltFailureKind.Security, cwdBase.Diagnostic?.Kind);

            // 3) Kontrol: sandbox'sız + CWD base → gerçekten CWD'deki secret okunur (vektör geçerli).
            var control = LayeredSandbox.Create(SandboxLayer.None).Run(xslt, baseUri: SecretFixture.CwdFileUri);
            output.WriteLine($"{name} None+CWD base: {control}");
            Assert.True(control.Leaked(Marker), $"{name} kontrolü sızmadı: {control}");
        }
    }

    [Fact]
    public void RelativeUri_ProductionBaseUri_NotHierarchical()
    {
        // urn:xsltcraft:stylesheet hiyerarşik değil → resolve-uri/static-base-uri yol vermez (ikinci, bağımsız katman).
        var r = TryRender(K3Vectors.Get("resolve-uri('.') base sızıntısı").Xslt(fx), "<r/>", Engine);
        Assert.Equal((XsltFailureKind.Runtime, "FORG0002"), (r.Diagnostic?.Kind, r.Diagnostic?.ErrorCode));
        Assert.DoesNotContain(Environment.CurrentDirectory, r.Diagnostic!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("<out>urn:xsltcraft:stylesheet</out>", Render(Stylesheet("<xsl:value-of select=\"static-base-uri()\"/>")));
    }

    // ---- yardımcılar -----------------------------------------------------------------------------

    private LayeredResult Run(SandboxLayer layers, string vectorName)
    {
        var v = K3Vectors.Get(vectorName);
        var r = OnBigStack(() => LayeredSandbox.Create(layers).Run(v.Xslt(fx), v.Xml?.Invoke(fx) ?? "<r/>"));
        output.WriteLine($"{layers} / {vectorName}: {r}");
        if (File.Exists(fx.ResultDocPath) && layers == SandboxLayer.None)
            File.Delete(fx.ResultDocPath);
        return r;
    }

    private LayeredResult AssertNoLeak(SandboxLayer layers, string vectorName)
    {
        var connections = fx.TotalConnections;
        var r = Run(layers, vectorName);
        Assert.False(r.Leaked(Marker), r.ToString());
        Assert.Equal(connections, fx.TotalConnections);
        return r;
    }
}
