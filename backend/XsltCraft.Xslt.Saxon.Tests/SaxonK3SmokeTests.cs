using Xunit.Abstractions;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// K3 temel sandbox smoke'u (üretim motoru, tam sandbox). Her vektör için: gerçek secret dosya içeriği
/// çıktıda/mesajda YOK, loopback dinleyiciye bağlantı YOK, result-document dosyası YOK ve hata BEKLENEN
/// katmandan (kod) geliyor — "her hata engel sayılır" zayıflığı yok (G0 O-3). Tam matris + "Full eksi X" T2.3'te.
/// </summary>
[Collection(nameof(SecretFixtureCollection))]
public sealed class SaxonK3SmokeTests(SecretFixture fx, ITestOutputHelper output)
{
    public enum Expect
    {
        /// <summary>Sandbox ihlali (Security) + belirli <see cref="SandboxCodes"/>.</summary>
        Security,

        /// <summary>Derleme reddi (ör. extension/dinamik kapalı, XPath 2.0 tavanı).</summary>
        CompileRejected,

        /// <summary>Çalışır ama boş/false döner (env, system-property, *-available).</summary>
        EmptyResult,
    }

    public static TheoryData<string> Vectors => new(VectorTable.Keys);

    private static readonly Dictionary<string, (Func<SecretFixture, string> Xslt, Expect Expect, string? Code, string? ExpectedOutput)> VectorTable = new()
    {
        ["doc(file)"] = (f => Stylesheet($"<xsl:copy-of select=\"doc('{SecretFixture.FileUri(f.SecretXml)}')\"/>"), Expect.Security, SandboxCodes.ResourceDenied, null),
        ["doc(http loopback)"] = (f => Stylesheet($"<xsl:copy-of select=\"doc('{f.HttpUri("x.xml")}')\"/>"), Expect.Security, SandboxCodes.ResourceDenied, null),
        ["doc(relative)"] = (f => Stylesheet("<xsl:copy-of select=\"doc('secret.xml')\"/>"), Expect.Security, SandboxCodes.ResourceDenied, null),
        ["document(file)"] = (f => Stylesheet($"<xsl:copy-of select=\"document('{SecretFixture.FileUri(f.SecretXml)}')\"/>"), Expect.Security, SandboxCodes.ResourceDenied, null),
        ["doc-available(file)"] = (f => Stylesheet($"<xsl:value-of select=\"doc-available('{SecretFixture.FileUri(f.SecretXml)}')\"/>"), Expect.EmptyResult, null, "<out>false</out>"),
        ["unparsed-text(file)"] = (f => Stylesheet($"<xsl:value-of select=\"unparsed-text('{SecretFixture.FileUri(f.SecretTxt)}')\"/>"), Expect.Security, SandboxCodes.UnparsedTextDenied, null),
        ["unparsed-text(http)"] = (f => Stylesheet($"<xsl:value-of select=\"unparsed-text('{f.HttpUri("x.txt")}')\"/>"), Expect.Security, SandboxCodes.UnparsedTextDenied, null),
        ["unparsed-text-available(file)"] = (f => Stylesheet($"<xsl:value-of select=\"unparsed-text-available('{SecretFixture.FileUri(f.SecretTxt)}')\"/>"), Expect.EmptyResult, null, "<out>false</out>"),
        ["collection(dir)"] = (f => Stylesheet($"<xsl:copy-of select=\"collection('{SecretFixture.FileUri(f.Dir)}?select=*.xml')\"/>"), Expect.Security, SandboxCodes.CollectionDenied, null),
        ["xsl:result-document"] = (f => Stylesheet($"<xsl:result-document href=\"{SecretFixture.FileUri(f.ResultDocPath)}\"><x>{SecretFixture.Marker}</x></xsl:result-document>"), Expect.CompileRejected, null, null),
        ["xsl:import(file)"] = (f => $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:import href=\"{SecretFixture.FileUri(f.SecretXsl)}\"/><xsl:template match=\"/\"><out><xsl:call-template name=\"leak\"/></out></xsl:template></xsl:stylesheet>", Expect.Security, SandboxCodes.ResourceDenied, null),
        ["xsl:include(file)"] = (f => $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:include href=\"{SecretFixture.FileUri(f.SecretXsl)}\"/><xsl:template match=\"/\"><out><xsl:call-template name=\"leak\"/></out></xsl:template></xsl:stylesheet>", Expect.Security, SandboxCodes.ResourceDenied, null),
        ["xsl:include(relative)"] = (f => $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:include href=\"secret.xsl\"/><xsl:template match=\"/\"><out/></xsl:template></xsl:stylesheet>", Expect.Security, SandboxCodes.ResourceDenied, null),
        ["xsl:import(http)"] = (f => $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:import href=\"{f.HttpUri("x.xsl")}\"/><xsl:template match=\"/\"><out/></xsl:template></xsl:stylesheet>", Expect.Security, SandboxCodes.ResourceDenied, null),
        ["environment-variable(secret)"] = (f => Stylesheet($"[<xsl:value-of select=\"environment-variable('{SecretFixture.EnvName}')\"/>]"), Expect.EmptyResult, null, "<out>[]</out>"),
        ["available-environment-variables()"] = (f => Stylesheet("[<xsl:value-of select=\"count(available-environment-variables())\"/>]"), Expect.EmptyResult, null, "<out>[0]</out>"),
        ["xsl:evaluate"] = (f => Stylesheet("<xsl:evaluate xpath=\"'1+1'\"/>", version: "3.0"), Expect.CompileRejected, null, null),
        ["system-property(java)"] = (f => Stylesheet("[<xsl:value-of select=\"system-property('user.dir'), system-property('java.home'), system-property('user.name')\" separator=\"\"/>]"), Expect.EmptyResult, null, "<out>[]</out>"),
        ["reflexive Q{java:}"] = (f => Stylesheet("<xsl:value-of select=\"Q{java:java.lang.System}getProperty('user.dir')\"/>"), Expect.CompileRejected, null, null),
        ["transform(stylesheet-location)"] = (f => Stylesheet($"<xsl:copy-of select=\"map:get(transform(map:merge((map:entry('stylesheet-location','{SecretFixture.FileUri(f.SecretXsl)}'), map:entry('source-node', /)))), 'output')\"/>", extraNs: "xmlns:map=\"http://www.w3.org/2005/xpath-functions/map\""), Expect.Security, SandboxCodes.ResourceDenied, null),
        ["transform(inner doc)"] = (f => Stylesheet($"<xsl:copy-of select=\"map:get(transform(map:merge((map:entry('stylesheet-text', '&lt;xsl:stylesheet version=&quot;2.0&quot; xmlns:xsl=&quot;{XslNs}&quot;&gt;&lt;xsl:template match=&quot;/&quot;&gt;&lt;r&gt;&lt;xsl:copy-of select=&quot;doc(&apos;&apos;{SecretFixture.FileUri(f.SecretXml)}&apos;&apos;)&quot;/&gt;&lt;/r&gt;&lt;/xsl:template&gt;&lt;/xsl:stylesheet&gt;'), map:entry('source-node', /)))), 'output')\"/>", extraNs: "xmlns:map=\"http://www.w3.org/2005/xpath-functions/map\""), Expect.Security, SandboxCodes.ResourceDenied, null),
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void FullSandbox_BlocksVector(string name)
    {
        var (xsltOf, expect, code, expectedOutput) = VectorTable[name];
        var xslt = xsltOf(fx);
        var before = fx.Connections;
        string result;
        XsltDiagnostic? diagnostic = null;
        try
        {
            result = Render(xslt);
        }
        catch (XsltProcessingException ex)
        {
            diagnostic = ex.Diagnostic;
            result = $"{ex.Diagnostic.Kind}/{ex.Diagnostic.ErrorCode}: {ex.Diagnostic.Message}";
        }

        output.WriteLine($"{name}: {result}");
        Assert.DoesNotContain(SecretFixture.Marker, result);
        Assert.Equal(before, fx.Connections);
        Assert.Equal(0, fx.Connections);
        Assert.False(File.Exists(fx.ResultDocPath));

        switch (expect)
        {
            case Expect.Security:
                Assert.NotNull(diagnostic);
                Assert.Equal(XsltFailureKind.Security, diagnostic!.Kind);
                Assert.Equal(code, diagnostic.ErrorCode);
                break;
            case Expect.CompileRejected:
                Assert.NotNull(diagnostic);
                Assert.True(diagnostic!.Kind is XsltFailureKind.Compile or XsltFailureKind.Security or XsltFailureKind.Unsupported,
                    diagnostic.Kind.ToString());
                break;
            case Expect.EmptyResult:
                Assert.Null(diagnostic);
                Assert.Equal(expectedOutput, result);
                break;
        }
    }
}

[CollectionDefinition(nameof(SecretFixtureCollection))]
public sealed class SecretFixtureCollection : ICollectionFixture<SecretFixture>
{
}
