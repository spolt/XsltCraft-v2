using System.Security;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Sandbox;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests.Sandbox;

/// <summary>
/// K3 vektörü. Beklenti BELİRLİDİR (G0 O-3): ya belirli <see cref="Kind"/> + <see cref="Code"/> ya da birebir
/// <see cref="Output"/> (boş/false sonuç). <see cref="Layer"/> Full sandbox'ta engeli koyan katmandır (rapor/okunurluk).
/// </summary>
public sealed record K3Vector(
    string Name,
    string Layer,
    Func<SecretFixture, string> Xslt,
    XsltFailureKind? Kind = null,
    string? Code = null,
    string? Output = null,
    Func<SecretFixture, string>? Xml = null);

/// <summary>Tam K3 vektör matrisi (G0 security-reviewer O-2 eksik listesi dahil).</summary>
internal static class K3Vectors
{
    public const string MapNs = "xmlns:map=\"http://www.w3.org/2005/xpath-functions/map\"";
    public const string SaxonNs = "xmlns:saxon=\"http://saxon.sf.net/\"";

    private const string Resolver = "Resolvers";
    private const string Protocols = "Protocols";
    private const string Env = "RuntimeIo(env)";
    private const string Dynamic = "Dynamic(AEF/evaluate)";
    private const string Dtd = "NoDtd";
    private const string XPath20 = "XPATH_VERSION_FOR_XSLT=20";
    private const string HeOnly = "Saxon-HE (EE özelliği yok)";
    private const string Bridge = "Girdi köprüsü (DtdProcessing.Prohibit)";
    private const string BaseUri = "urn: base URI (hiyerarşik değil)";
    private const string XIncludeOff = "XINCLUDE=false";

    /// <summary>xi:include işlenmez; literal sonuç elemanı olarak kopyalanır (href içeriği okunmaz).</summary>
    public const string XiLiteral = "XI_LITERAL";

    private const string Res = SandboxCodes.ResourceDenied;
    private const string Txt = SandboxCodes.UnparsedTextDenied;
    private const string Col = SandboxCodes.CollectionDenied;
    private const string Policy = SandboxCodes.SaxonPolicy;
    private const string Default = "Saxon varsayılanı";

    public static string Copy(string expr) => Stylesheet($"<xsl:copy-of select=\"{expr}\"/>", extraNs: MapNs);

    public static string Value(string expr) => Stylesheet($"[<xsl:value-of select=\"{expr}\"/>]", extraNs: MapNs);

    public static string WithTop(string href, string element) =>
        $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:{element} href=\"{href}\"/><xsl:template match=\"/\"><out/></xsl:template></xsl:stylesheet>";

    /// <summary>transform() iç stylesheet'i (XPath 2.0 tavanında: map:merge/map:entry/map:get, initial-template yok).</summary>
    public static string Transform(string innerBody, string innerTop = "") =>
        Copy("map:get(transform(map:merge((map:entry('stylesheet-text', '" + Inner(innerBody, innerTop) + "'), map:entry('source-node', /)))), 'output')");

    public static string Inner(string body, string top = "") =>
        SecurityElement.Escape($"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\">{top}<xsl:template match=\"/\"><r>{body}</r></xsl:template></xsl:stylesheet>")
            .Replace("&apos;", "''", StringComparison.Ordinal); // XPath '...' literali içinde tek tırnak

    private static string F(string p) => SecretFixture.FileUri(p);

    private static string Unc(string path) =>
        "file://127.0.0.1/" + path.Replace(":", "$", StringComparison.Ordinal).Replace('\\', '/');

    public static readonly K3Vector[] All =
    [
        // ---- xsl:include / xsl:import ------------------------------------------------------------
        new("xsl:include(abs file)", Resolver, f => WithTop(F(f.SecretXsl), "include"), XsltFailureKind.Security, Res),
        new("xsl:include(relative)", Resolver, f => WithTop("secret.xsl", "include"), XsltFailureKind.Security, Res),
        new("xsl:include(http)", Resolver, f => WithTop(f.LiveHttpUri("i.xsl"), "include"), XsltFailureKind.Security, Res),
        new("xsl:import(abs file)", Resolver, f => WithTop(F(f.SecretXsl), "import"), XsltFailureKind.Security, Res),
        new("xsl:import(relative)", Resolver, f => WithTop("secret.xsl", "import"), XsltFailureKind.Security, Res),
        new("xsl:import(http)", Resolver, f => WithTop(f.LiveHttpUri("i.xsl"), "import"), XsltFailureKind.Security, Res),

        // ---- doc / document ------------------------------------------------------------------------
        new("doc(file)", Resolver, f => Copy($"doc('{F(f.SecretXml)}')"), XsltFailureKind.Security, Res),
        new("doc(http live)", Resolver, f => Copy($"doc('{f.LiveHttpUri("d.xml")}')"), XsltFailureKind.Security, Res),
        new("doc(http silent)", Resolver, f => Copy($"doc('{f.HttpUri("d.xml")}')"), XsltFailureKind.Security, Res),
        new("document(file)", Resolver, f => Copy($"document('{F(f.SecretXml)}')"), XsltFailureKind.Security, Res),
        new("document(http live)", Resolver, f => Copy($"document('{f.LiveHttpUri("d.xml")}')"), XsltFailureKind.Security, Res),
        new("doc-available(file)", Resolver, f => Value($"doc-available('{F(f.SecretXml)}')"), Output: "<out>[false]</out>"),
        new("doc-available(http live)", Resolver, f => Value($"doc-available('{f.LiveHttpUri("d.xml")}')"), Output: "<out>[false]</out>"),

        // ---- metin / json ------------------------------------------------------------------------
        new("unparsed-text(file)", Resolver, f => Value($"unparsed-text('{F(f.SecretTxt)}')"), XsltFailureKind.Security, Txt),
        new("unparsed-text(http live)", Resolver, f => Value($"unparsed-text('{f.LiveHttpUri("t.txt")}')"), XsltFailureKind.Security, Txt),
        new("unparsed-text-lines(file)", Resolver, f => Value($"unparsed-text-lines('{F(f.SecretTxt)}')"), XsltFailureKind.Security, Txt),
        new("unparsed-text-available(file)", Resolver, f => Value($"unparsed-text-available('{F(f.SecretTxt)}')"), Output: "<out>[false]</out>"),
        new("json-doc(file)", Resolver, f => Value($"map:get(json-doc('{F(f.SecretJson)}'), 'k')"), XsltFailureKind.Security, Txt),

        // ---- collection --------------------------------------------------------------------------
        new("collection(dir)", Resolver, f => Copy($"collection('{F(f.Dir)}?select=*.xml')"), XsltFailureKind.Security, Col),
        new("uri-collection(dir)", Resolver, f => Value($"uri-collection('{F(f.Dir)}?select=*.xml')"), XsltFailureKind.Security, Col),
        new("collection() argümansız", Default + " (varsayılan koleksiyon yok)", f => Value("count(collection())"), XsltFailureKind.Runtime, "FODC0002"),

        // ---- XSLT 3.0 G/Ç komutları --------------------------------------------------------------
        new("xsl:source-document", Resolver, f => Stylesheet($"<xsl:source-document href=\"{F(f.SecretXml)}\" streamable=\"no\"><xsl:copy-of select=\".\"/></xsl:source-document>"), XsltFailureKind.Security, Res),
        new("xsl:merge for-each-source", Resolver, f => Stylesheet($"<xsl:merge><xsl:merge-source for-each-source=\"'{F(f.SecretXml)}'\" select=\"/s\"><xsl:merge-key select=\"string(.)\"/></xsl:merge-source><xsl:merge-action><xsl:copy-of select=\"current-merge-group()\"/></xsl:merge-action></xsl:merge>"), XsltFailureKind.Security, Res),
        new("xsl:result-document(file)", Dynamic, f => Stylesheet($"<xsl:result-document href=\"{F(f.ResultDocPath)}\"><x>{SecretFixture.Marker}</x></xsl:result-document>"), XsltFailureKind.Security, Policy),
        new("xsl:result-document(relative)", Dynamic, f => Stylesheet($"<xsl:result-document href=\"out.xml\"><x>{SecretFixture.Marker}</x></xsl:result-document>"), XsltFailureKind.Security, Policy),

        // ---- transform() -------------------------------------------------------------------------
        new("transform(stylesheet-location)", Resolver, f => Copy($"map:get(transform(map:merge((map:entry('stylesheet-location','{F(f.SecretXsl)}'), map:entry('source-node', /)))), 'output')"), XsltFailureKind.Security, Res),
        new("transform(stylesheet-text) iç doc()", Resolver, f => Transform($"<xsl:copy-of select=\"doc('{F(f.SecretXml)}')\"/>"), XsltFailureKind.Security, Res),
        new("transform(stylesheet-text) iç unparsed-text()", Resolver, f => Transform($"<xsl:value-of select=\"unparsed-text('{F(f.SecretTxt)}')\"/>"), XsltFailureKind.Security, Txt),
        new("transform(stylesheet-text) iç env", Env, f => Transform($"[<xsl:value-of select=\"environment-variable('{SecretFixture.EnvName}')\"/>]"), Output: "<out><r>[]</r></out>"),

        // ---- XQuery modülü / şema / paket --------------------------------------------------------
        new("load-xquery-module", Resolver, f => Value($"map:get(load-xquery-module('urn:m', map:entry('location-hints', '{F(f.SecretXq)}')), 'functions')"), XsltFailureKind.Security, Res),
        new("xsl:import-schema(file)", HeOnly, f => Stylesheet("x", top: $"<xsl:import-schema namespace=\"urn:s\" schema-location=\"{F(f.SecretXsd)}\"/>"), XsltFailureKind.Compile, "XTSE1650"),
        new("xsl:import-schema(http live)", HeOnly, f => Stylesheet("x", top: $"<xsl:import-schema namespace=\"urn:s\" schema-location=\"{f.LiveHttpUri("s.xsd")}\"/>"), XsltFailureKind.Compile, "XTSE1650"),
        new("xsl:use-package", HeOnly + " (paket deposu yok)", f => Stylesheet("x", top: $"<xsl:use-package name=\"{F(f.SecretXsl)}\"/>"), XsltFailureKind.Compile, "XTSE3000"),
        new("xi:include(stylesheet)", XIncludeOff, f => Stylesheet($"<xi:include href=\"{F(f.SecretTxt)}\" parse=\"text\"/>", extraNs: "xmlns:xi=\"http://www.w3.org/2001/XInclude\""), Output: XiLiteral),

        // ---- parse-xml / DTD ---------------------------------------------------------------------
        new("parse-xml + harici entity", Dtd, f => Value($"parse-xml('&lt;!DOCTYPE x [&lt;!ENTITY e SYSTEM &quot;{F(f.SecretTxt)}&quot;&gt;]&gt;&lt;x&gt;&amp;e;&lt;/x&gt;')"), XsltFailureKind.Runtime, "FODC0006"),
        new("parse-xml billion laughs", Dtd, f => Value("string-length(string(parse-xml('&lt;!DOCTYPE x [&lt;!ENTITY a &quot;aaaaaaaaaa&quot;&gt;&lt;!ENTITY b &quot;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&amp;a;&quot;&gt;&lt;!ENTITY c &quot;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&amp;b;&quot;&gt;]&gt;&lt;x&gt;&amp;c;&lt;/x&gt;')))"), XsltFailureKind.Runtime, "FODC0006"),
        new("parse-xml-fragment + DTD", "JDK ayrıştırıcısı (fragment içinde DOCTYPE geçersiz)", f => Value($"parse-xml-fragment('&lt;!DOCTYPE x [&lt;!ENTITY e SYSTEM &quot;{F(f.SecretTxt)}&quot;&gt;]&gt;&lt;x&gt;&amp;e;&lt;/x&gt;')"), XsltFailureKind.Runtime, "FODC0006"),
        new("girdi XML DTD/XXE", Bridge, f => Copy("/"), XsltFailureKind.Xml, null, Xml: f => $"<!DOCTYPE r [<!ENTITY e SYSTEM \"{F(f.SecretTxt)}\">]><r>&e;</r>"),
        new("stylesheet DTD (iç entity)", Dtd + " (.NET ön okuma)", f => $"<!DOCTYPE xsl:stylesheet [ <!ENTITY e \"{SecretFixture.Marker}\"> ]><xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"><o>&e;</o></xsl:template></xsl:stylesheet>", XsltFailureKind.Security, SandboxCodes.StylesheetDtd),
        new("stylesheet DTD (harici entity)", Dtd + " (.NET ön okuma)", f => $"<!DOCTYPE xsl:stylesheet [ <!ENTITY e SYSTEM \"{F(f.SecretTxt)}\"> ]><xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"><o>&e;</o></xsl:template></xsl:stylesheet>", XsltFailureKind.Security, SandboxCodes.StylesheetDtd),

        // ---- ortam / sistem özellikleri ----------------------------------------------------------
        new("environment-variable(secret)", Env, f => Value($"environment-variable('{SecretFixture.EnvName}')"), Output: "<out>[]</out>"),
        new("available-environment-variables()", Env, f => Value("count(available-environment-variables())"), Output: "<out>[0]</out>"),
        new("static değişken environment-variable", Env, f => Stylesheet("[<xsl:value-of select=\"$s\"/>]", top: $"<xsl:variable name=\"s\" static=\"yes\" select=\"environment-variable('{SecretFixture.EnvName}')\"/>", version: "3.0"), Output: "<out>[]</out>"),
        new("function-lookup(environment-variable)", XPath20 + " (dinamik çağrı)", f => Value($"function-lookup(QName('http://www.w3.org/2005/xpath-functions','environment-variable'), 1)('{SecretFixture.EnvName}')"), XsltFailureKind.Unsupported, "XPST0003"),
        new("system-property(user.dir)", Dynamic, f => Value("system-property('user.dir')"), Output: "<out>[]</out>"),
        new("system-property(java.home)", Dynamic, f => Value("system-property('java.home')"), Output: "<out>[]</out>"),
        new("system-property(os.name)", Dynamic, f => Value("system-property('os.name')"), Output: "<out>[]</out>"),
        new("system-property(java.io.tmpdir)", Dynamic, f => Value("system-property('java.io.tmpdir')"), Output: "<out>[]</out>"),

        // ---- dinamik kod / uzantılar -------------------------------------------------------------
        new("xsl:evaluate", XPath20, f => Stylesheet($"<xsl:evaluate xpath=\"'doc(&quot;{F(f.SecretXml)}&quot;)'\"/>", version: "3.0"), XsltFailureKind.Unsupported, "XPST0003"),
        new("reflexive Q{java:}", XPath20 + " (EQName yok)", f => Value("Q{java:java.lang.System}getProperty('user.dir')"), XsltFailureKind.Compile, "XPST0003"),
        new("reflexive java: önekli", Dynamic, f => Value("j:getProperty('user.dir')").Replace("<xsl:stylesheet ", "<xsl:stylesheet xmlns:j=\"java:java.lang.System\" ", StringComparison.Ordinal), XsltFailureKind.Compile, "XPST0017"),
        new("reflexive Q{clitype:}", XPath20 + " (EQName yok)", f => Value("Q{clitype:System.Environment}get_MachineName()"), XsltFailureKind.Compile, "XPST0003"),
        new("reflexive clitype: önekli", Dynamic, f => Value("c:get_MachineName()").Replace("<xsl:stylesheet ", "<xsl:stylesheet xmlns:c=\"clitype:System.Environment\" ", StringComparison.Ordinal), XsltFailureKind.Compile, "XPST0017"),
        new("function-available(java:)", Dynamic, f => Value("function-available('j:getProperty', 1)").Replace("<xsl:stylesheet ", "<xsl:stylesheet xmlns:j=\"java:java.lang.System\" ", StringComparison.Ordinal), Output: "<out>[false]</out>"),
        new("saxon:while uzantı komutu", HeOnly, f => Stylesheet("<saxon:while test=\"true()\" xsl:extension-element-prefixes=\"saxon\"><x/></saxon:while>", extraNs: SaxonNs), XsltFailureKind.Runtime, "XTDE1450"),
        new("saxon:evaluate", Dynamic + " + " + HeOnly, f => Value("saxon:evaluate('1+1')").Replace("<xsl:stylesheet ", $"<xsl:stylesheet {SaxonNs} ", StringComparison.Ordinal), XsltFailureKind.Compile, "XPST0017"),
        new("saxon:next-in-chain", HeOnly, f => Stylesheet("x", top: $"<xsl:output saxon:next-in-chain=\"{F(f.SecretXsl)}\"/>", extraNs: SaxonNs), XsltFailureKind.Compile, "XTSE0020"),

        // ---- derleme anı -------------------------------------------------------------------------
        new("use-when doc() (derleme)", Resolver, f => Stylesheet("<x/>", top: $"<xsl:template name=\"t\" use-when=\"exists(doc('{F(f.SecretXml)}'))\"/>"), XsltFailureKind.Security, Res),
        new("use-when unparsed-text() (derleme)", Resolver, f => Stylesheet("<x/>", top: $"<xsl:template name=\"t\" use-when=\"unparsed-text('{F(f.SecretTxt)}') = ''\"/>"), XsltFailureKind.Security, Txt),
        new("static değişken unparsed-text() (derleme)", Resolver, f => Stylesheet("<xsl:value-of select=\"$s\"/>", top: $"<xsl:variable name=\"s\" static=\"yes\" select=\"unparsed-text('{F(f.SecretTxt)}')\"/>", version: "3.0"), XsltFailureKind.Security, Txt),

        // ---- dolaylı çağrı biçimleri -------------------------------------------------------------
        new("function-lookup(doc)", XPath20 + " (dinamik çağrı)", f => Copy($"function-lookup(QName('http://www.w3.org/2005/xpath-functions','doc'), 1)('{F(f.SecretXml)}')"), XsltFailureKind.Unsupported, "XPST0003"),
        new("doc#1 adlı referans", XPath20, f => Copy($"doc#1('{F(f.SecretXml)}')"), XsltFailureKind.Unsupported, "XPST0003"),
        new("Q{fn}doc EQName", XPath20 + " (EQName yok)", f => Copy($"Q{{http://www.w3.org/2005/xpath-functions}}doc('{F(f.SecretXml)}')"), XsltFailureKind.Compile, "XPST0003"),

        // ---- URI şemaları ------------------------------------------------------------------------
        new("doc(classpath:)", Resolver, f => Copy("doc('classpath:org/xmlresolver/catalog.xml')"), XsltFailureKind.Security, Res),
        new("unparsed-text(classpath:)", Resolver, f => Value("unparsed-text('classpath:META-INF/MANIFEST.MF')"), XsltFailureKind.Security, Txt),
        new("doc(jar:)", Resolver, f => Copy($"doc('jar:{F(typeof(net.sf.saxon.s9api.Processor).Assembly.Location)}!/x.xml')"), XsltFailureKind.Security, Res),
        new("doc(data:)", Resolver, f => Copy("doc('data:application/xml,%3Cd%3EDATA_OK%3C/d%3E')"), XsltFailureKind.Security, Res),
        new("doc(UNC file://127.0.0.1/C$/)", Resolver, f => Copy($"doc('{Unc(f.SecretXml)}')"), XsltFailureKind.Security, Res),
        new("doc(çıplak C:/)", Resolver, f => Copy($"doc('{f.SecretXml.Replace('\\', '/')}')"), XsltFailureKind.Security, Res),
        new("doc(FILE: büyük harf)", Resolver, f => Copy($"doc('{F(f.SecretXml).Replace("file:", "FILE:", StringComparison.Ordinal)}')"), XsltFailureKind.Security, Res),
        new("doc(relative)", Resolver, f => Copy("doc('secret.xml')"), XsltFailureKind.Security, Res),
        new("resolve-uri('.') base sızıntısı", BaseUri, f => Value("resolve-uri('.')"), XsltFailureKind.Runtime, "FORG0002"),
    ];

    public static K3Vector Get(string name) => All.Single(v => v.Name == name);
}
