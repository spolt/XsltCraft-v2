using net.sf.saxon.lib;
using net.sf.saxon.s9api;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon;

/// <summary>
/// Saxon sandbox yapılandırmasının TEK kaynağı (ADR 008 §6). İki profil (<see cref="SaxonProfile"/>) aynı
/// sandbox'ı kullanır; yalnız sınırlar <see cref="SaxonEngineOptions"/>'tan gelir. Katmanlar:
/// <list type="bullet">
/// <item>Protokoller: <see cref="Feature.ALLOWED_PROTOCOLS"/> = <see cref="AllowedProtocolsValue"/> (TUZAK: <c>""</c> = "all").</item>
/// <item>G/Ç: reddeden ResourceResolver (config + compiler + transformer), UnparsedTextURIResolver,
/// CollectionFinder, ResultDocumentHandler; boş EnvironmentVariableResolver.</item>
/// <item>Dinamik kod: DISABLE_XSL_EVALUATE, ALLOW_EXTERNAL_FUNCTIONS=false, ALLOW_SYNTAX_EXTENSIONS=false.</item>
/// <item>Dil tavanı: XPATH_VERSION_FOR_XSLT=20 (let, satır içi function(), !, map sözdizimi derlemede red).</item>
/// <item>Collation: <see cref="RestrictedCollationResolver"/> (izin listesi; <c>?class=</c> sınıf yüklemesi kapalı).</item>
/// <item>Ayrıştırıcı: disallow-doctype-decl (parse-xml vb.); stylesheet için ayrıca <c>StylesheetSource</c>.</item>
/// <item>ReDoS: REGEX_BACKTRACKING_LIMIT. Çıktı kanalları: sessiz Logger, sınırlı ErrorReporter/MessageHandler,
/// <c>trace()</c> hedefi null — stderr'e hiçbir şey gitmez.</item>
/// </list>
/// </summary>
public sealed class SaxonProcessorFactory
{
    /// <summary>
    /// Var olmayan tek bir şema adı → hiçbir protokol izinli değil. <c>"-"</c> adayı da test edildi
    /// (<c>AllowedProtocols_Value_BlocksAllSchemes</c>); ikisi de engelliyor, okunur olduğu için bu seçildi.
    /// </summary>
    public const string AllowedProtocolsValue = "xsltcraft-none";

    internal const string DisallowDoctypeFeature = "http://apache.org/xml/features/disallow-doctype-decl";
    internal const int XPathVersionForXslt = 20;

    public SaxonProcessorFactory(SaxonEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        Options = options;
    }

    public SaxonEngineOptions Options { get; }

    /// <summary>Sandbox'lı yeni <see cref="Processor"/> (HE, şema-farkında değil). Thread-safe; motor başına bir tane.</summary>
    public Processor CreateProcessor()
    {
        var processor = new Processor(false);
        var config = processor.getUnderlyingConfiguration();

        // JAXP güvenli işleme modu önce (kendi ayarlarını yapar); aşağıdaki açık ayarlar her durumda üstündedir.
        SecureProcessing.Apply(config);

        processor.setConfigurationProperty(Feature.ALLOWED_PROTOCOLS, AllowedProtocolsValue);
        config.setResourceResolver(DenyingResourceResolver.Instance);
        config.setUnparsedTextURIResolver(DenyingUnparsedTextResolver.Instance);
        config.setCollectionFinder(DenyingCollectionFinder.Instance);
        processor.setConfigurationProperty(Feature.ENVIRONMENT_VARIABLE_RESOLVER, EmptyEnvironmentVariableResolver.Instance);

        processor.setConfigurationProperty(Feature.DISABLE_XSL_EVALUATE, java.lang.Boolean.TRUE);
        processor.setConfigurationProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, java.lang.Boolean.FALSE);
        processor.setConfigurationProperty(Feature.ALLOW_SYNTAX_EXTENSIONS, java.lang.Boolean.FALSE);
        processor.setConfigurationProperty(Feature.XINCLUDE, java.lang.Boolean.FALSE);
        processor.setConfigurationProperty(Feature.DTD_VALIDATION, java.lang.Boolean.FALSE);
        processor.setConfigurationProperty(Feature.XPATH_VERSION_FOR_XSLT, java.lang.Integer.valueOf(XPathVersionForXslt));
        processor.setConfigurationProperty(Feature.REGEX_BACKTRACKING_LIMIT, java.lang.Integer.valueOf(Options.RegexBacktrackingLimit));

        config.setCollationURIResolver(new RestrictedCollationResolver(config.getCollationURIResolver()));
        config.setParseOptions(config.getParseOptions().withParserFeature(DisallowDoctypeFeature, true));
        config.setLogger(SilentLogger.Instance);

        return processor;
    }

    /// <summary>
    /// Derleyici: reddeden resolver, sınırlı hata raporlayıcı ve kalıcı trace derlemesi
    /// (<c>DepthLimitingTraceListener</c>'ın <c>enter()</c> alabilmesi için şart).
    /// </summary>
    internal XsltCompiler CreateCompiler(Processor processor, ErrorReporter errorReporter)
    {
        var compiler = processor.newXsltCompiler();
        compiler.setResourceResolver(DenyingResourceResolver.Instance);
        compiler.setErrorReporter(errorReporter);
        compiler.setCompileWithTracing(true);
        return compiler;
    }

    /// <summary>Transform başına yeni transformer; listener, mesaj ve hata işleyicileri de transform başınadır.</summary>
    internal Xslt30Transformer CreateTransformer(
        XsltExecutable executable,
        TraceListener listener,
        java.util.function.Consumer messageHandler,
        ErrorReporter errorReporter)
    {
        var transformer = executable.load30();
        transformer.setResourceResolver(DenyingResourceResolver.Instance);
        transformer.setResultDocumentHandler(DenyingResultDocumentHandler.Instance);
        transformer.setErrorReporter(errorReporter);
        transformer.setMessageHandler(messageHandler);
        transformer.setTraceListener(listener);
        transformer.setTraceFunctionDestination(null);
        return transformer;
    }
}
