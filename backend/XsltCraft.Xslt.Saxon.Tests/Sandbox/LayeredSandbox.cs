using net.sf.saxon.lib;
using XsltCraft.Application.Xslt;
using net.sf.saxon.s9api;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Diagnostics;
using XsltCraft.Xslt.Saxon.Input;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon.Tests.Sandbox;

/// <summary>Sandbox'ın bağımsız olarak çıkarılabilen katmanları (ADR 008 §6).</summary>
[Flags]
public enum SandboxLayer
{
    None = 0,

    /// <summary>Reddeden ResourceResolver (config + derleyici + transformer), UnparsedTextURIResolver, CollectionFinder.</summary>
    Resolvers = 1,

    /// <summary><c>Feature.ALLOWED_PROTOCOLS = "xsltcraft-none"</c>.</summary>
    Protocols = 2,

    /// <summary>Çalışma anı G/Ç kancaları: ResultDocumentHandler (red) + boş EnvironmentVariableResolver.</summary>
    RuntimeIo = 4,

    /// <summary>Dinamik kod: DISABLE_XSL_EVALUATE, ALLOW_EXTERNAL_FUNCTIONS=false (+ JAXP secure processing), ALLOW_SYNTAX_EXTENSIONS=false.</summary>
    Dynamic = 8,

    /// <summary>DTD reddi: .NET ön okuma + stylesheet SAX disallow-doctype + Configuration parse seçenekleri (parse-xml).</summary>
    NoDtd = 16,

    /// <summary><c>Feature.XPATH_VERSION_FOR_XSLT = 20</c> (let, satır içi function(), dinamik çağrı, <c>Q{}</c> EQName, xsl:evaluate derlemede red).</summary>
    XPath20 = 32,

    Full = Resolvers | Protocols | RuntimeIo | Dynamic | NoDtd | XPath20,
}

/// <summary>
/// TEST SEAM: <see cref="SaxonProcessorFactory.CreateProcessor"/>'ın katman katman yeniden kurulumu — üretim API'sine
/// dokunmadan "Full eksi X" ve sandbox'sız kontrol koşuları için. Katman dışı ayarlar (XINCLUDE=false, DTD_VALIDATION=false, collation izin listesi, regex sınırı, sessiz logger/raporlayıcı) her
/// varyantta ÜRETİMLE AYNIDIR; böylece fark yalnız çıkarılan katmandır. <c>Full</c> varyantın üretimle aynı
/// yapılandırmayı ürettiği <c>LayeredSandbox_Full_MatchesProductionFactory</c> ile kilitlenir (sapma = kırmızı).
/// </summary>
internal sealed class LayeredSandbox
{
    private static readonly SaxonEngineOptions Options = SaxonEngineOptions.Isolated;

    private LayeredSandbox(SandboxLayer layers, Processor processor)
    {
        Layers = layers;
        Processor = processor;
    }

    public SandboxLayer Layers { get; }

    public Processor Processor { get; }

    public static LayeredSandbox Create(SandboxLayer layers)
    {
        var processor = new Processor(false);
        var config = processor.getUnderlyingConfiguration();

        if (layers.HasFlag(SandboxLayer.Dynamic))
        {
            SecureProcessing.Apply(config);
            processor.setConfigurationProperty(Feature.DISABLE_XSL_EVALUATE, java.lang.Boolean.TRUE);
            processor.setConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, java.lang.Boolean.FALSE);
            processor.setConfigurationProperty(Feature.ALLOW_SYNTAX_EXTENSIONS, java.lang.Boolean.FALSE);
        }

        if (layers.HasFlag(SandboxLayer.Protocols))
            processor.setConfigurationProperty(Feature.ALLOWED_PROTOCOLS, SaxonProcessorFactory.AllowedProtocolsValue);

        if (layers.HasFlag(SandboxLayer.Resolvers))
        {
            config.setResourceResolver(DenyingResourceResolver.Instance);
            config.setUnparsedTextURIResolver(DenyingUnparsedTextResolver.Instance);
            config.setCollectionFinder(DenyingCollectionFinder.Instance);
        }

        if (layers.HasFlag(SandboxLayer.RuntimeIo))
            processor.setConfigurationProperty(Feature.ENVIRONMENT_VARIABLE_RESOLVER, EmptyEnvironmentVariableResolver.Instance);

        // Katman dışı (her varyantta üretimle aynı).
        processor.setConfigurationProperty(Feature.XINCLUDE, java.lang.Boolean.FALSE);
        processor.setConfigurationProperty(Feature.DTD_VALIDATION, java.lang.Boolean.FALSE);
        if (layers.HasFlag(SandboxLayer.XPath20))
            processor.setConfigurationProperty(Feature.XPATH_VERSION_FOR_XSLT, java.lang.Integer.valueOf(SaxonProcessorFactory.XPathVersionForXslt));
        processor.setConfigurationProperty(Feature.REGEX_BACKTRACKING_LIMIT, java.lang.Integer.valueOf(Options.RegexBacktrackingLimit));
        config.setCollationURIResolver(new RestrictedCollationResolver(config.getCollationURIResolver()));

        // Üretimdeki sırayla (XINCLUDE ayarı parse seçeneklerine yazılır; sonra disallow-doctype eklenir).
        if (layers.HasFlag(SandboxLayer.NoDtd))
            config.setParseOptions(config.getParseOptions().withParserFeature(SaxonProcessorFactory.DisallowDoctypeFeature, true));

        config.setLogger(SilentLogger.Instance);

        return new LayeredSandbox(layers, processor);
    }

    /// <summary>
    /// Derle + dönüştür. Başarı → <c>OK:&lt;çıktı&gt;</c>; hata → <see cref="XsltDiagnostic"/> (üretim eşlemesi
    /// <see cref="SaxonDiagnostics"/>). <paramref name="baseUri"/> verilirse stylesheet o systemId ile derlenir
    /// (üretimde her zaman <c>urn:xsltcraft:stylesheet</c>).
    /// </summary>
    public LayeredResult Run(string xslt, string xml = "<r/>", string? baseUri = null)
    {
        var errors = new BoundedErrorReporter(Options.MaxRetainedErrors, Options.MaxMessageChars);
        XsltExecutable executable;
        try
        {
            if (Layers.HasFlag(SandboxLayer.NoDtd))
                StylesheetSource.PreRead(xslt, 256, Options.MaxStylesheetCharacters, screenStaticExpressions: false); // Saxon katmanları tek tek ölçülür; statik ifade reddi üretim motorunda (K3 matrisi)

            var compiler = Processor.newXsltCompiler();
            if (Layers.HasFlag(SandboxLayer.Resolvers))
                compiler.setResourceResolver(DenyingResourceResolver.Instance);
            compiler.setErrorReporter(errors);
            executable = compiler.compile(Source(xslt, baseUri));
        }
        catch (XsltProcessingException ex)
        {
            return LayeredResult.Failed(ex.Diagnostic, "pre-read");
        }
        catch (Exception ex)
        {
            return LayeredResult.Failed(SaxonDiagnostics.FromCompile(ex, errors.Errors), "compile");
        }

        var budget = new ExecutionBudget(TimeSpan.FromSeconds(20), CancellationToken.None);
        try
        {
            var transformer = executable.load30();
            if (Layers.HasFlag(SandboxLayer.Resolvers))
                transformer.setResourceResolver(DenyingResourceResolver.Instance);
            if (Layers.HasFlag(SandboxLayer.RuntimeIo))
                transformer.setResultDocumentHandler(DenyingResultDocumentHandler.Instance);
            transformer.setErrorReporter(new BoundedErrorReporter(Options.MaxRetainedErrors, Options.MaxMessageChars));
            transformer.setMessageHandler(new BoundedMessageHandler(4, 256));
            transformer.setTraceFunctionDestination(null);

            XdmNode doc;
            try
            {
                var bridge = new XmlReaderTreeBridge(Processor, executable.getWhitespaceStrippingPolicy());
                doc = bridge.Build(xml, new TreeBridgeLimits(512, 1_000_000, Options.MaxInputCharacters));
            }
            catch (System.Xml.XmlException ex)
            {
                return LayeredResult.Failed(XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Xml, XsltEngineKind.Saxon), "input");
            }

            var sw = new StringWriter();
            transformer.setGlobalContextItem(doc);
            transformer.applyTemplates(doc, transformer.newSerializer(new LimitedJavaWriter(sw, 1_000_000, budget)));
            return LayeredResult.Ran(sw.ToString());
        }
        catch (Exception ex)
        {
            return LayeredResult.Failed(SaxonDiagnostics.FromRuntime(ex, budget.Tripped), "runtime");
        }
    }

    private javax.xml.transform.Source Source(string xslt, string? baseUri)
    {
        if (Layers.HasFlag(SandboxLayer.NoDtd))
        {
            var source = StylesheetSource.Create(xslt);
            if (baseUri is not null)
                source.setSystemId(baseUri); // SAXSource → InputSource systemId da değişir
            return source;
        }

        // NoDtd yoksa JDK'nın varsayılan (DOCTYPE'a izin veren) ayrıştırıcısı.
        return new javax.xml.transform.stream.StreamSource(new java.io.StringReader(xslt), baseUri ?? StylesheetSource.SystemId);
    }
}

/// <summary>Katmanlı koşu sonucu.</summary>
internal sealed record LayeredResult(string? Output, XsltDiagnostic? Diagnostic, string? Phase)
{
    public static LayeredResult Ran(string output) => new(output, null, null);

    public static LayeredResult Failed(XsltDiagnostic diagnostic, string phase) => new(null, diagnostic, phase);

    public bool Succeeded => Diagnostic is null;

    public bool Leaked(string marker) =>
        (Output?.Contains(marker, StringComparison.Ordinal) ?? false) ||
        (Diagnostic?.Message.Contains(marker, StringComparison.Ordinal) ?? false);

    public override string ToString() =>
        Succeeded ? "OK:" + Output : $"{Diagnostic!.Kind}/{Diagnostic.ErrorCode}@{Phase}: {Diagnostic.Message}";
}
