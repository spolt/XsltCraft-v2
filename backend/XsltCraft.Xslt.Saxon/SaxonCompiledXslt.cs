using System.Diagnostics;
using System.Xml;
using net.sf.saxon.s9api;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Diagnostics;
using XsltCraft.Xslt.Saxon.Input;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon;

/// <summary>Bir dönüşümün iç gözlemleri (ölçüm/test; üretim çağrıları kullanmaz).</summary>
internal sealed record SaxonTransformProbe(
    int MaxTraceDepthSeen,
    long TraceEnterCount,
    long MessageCount,
    IReadOnlyList<string> RetainedMessages,
    long OutputChars,
    SaxonLimitError? Tripped);

/// <summary>
/// Derlenmiş Saxon stylesheet'i (<see cref="XsltExecutable"/> thread-safe). Her <see cref="Transform"/> kendi
/// transformer'ını, trace listener'ını, bütçesini, çıktı yazıcısını ve mesaj/hata işleyicilerini kurar —
/// dönüşümler arasında paylaşılan değişebilir durum yoktur.
/// </summary>
internal sealed class SaxonCompiledXslt : ICompiledXslt
{
    private readonly XsltExecutable _executable;
    private readonly SaxonProcessorFactory _factory;
    private readonly XmlReaderTreeBridge _bridge;

    public SaxonCompiledXslt(XsltExecutable executable, Processor processor, SaxonProcessorFactory factory, long compileMs)
    {
        _executable = executable;
        _factory = factory;
        CompileMs = compileMs;
        // xsl:strip-space / preserve-space: ağaç kendimiz kurduğumuz için politika açıkça verilmeli.
        _bridge = new XmlReaderTreeBridge(processor, executable.getWhitespaceStrippingPolicy());
    }

    public XsltEngineKind Kind => XsltEngineKind.Saxon;

    /// <summary>Derleme süresi (ms) — ölçüm için; sözleşme gereği Transform'un CompileMs'i 0'dır.</summary>
    public long CompileMs { get; }

    public XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits) =>
        Transform(xml, output, limits, probe: null);

    internal XsltTransformTimings Transform(string xml, TextWriter output, XsltExecutionLimits limits, Action<SaxonTransformProbe>? probe)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(limits);

        var options = _factory.Options;
        var budget = new ExecutionBudget(limits.Deadline, limits.Cancellation);
        var listener = new DepthLimitingTraceListener(options.MaxTraceDepth, budget);
        var messages = new BoundedMessageHandler(options.MaxRetainedMessages, options.MaxMessageChars);
        var errors = new BoundedErrorReporter(options.MaxRetainedErrors, options.MaxMessageChars);
        var writer = new LimitedJavaWriter(output, limits.MaxOutputChars, budget);

        try
        {
            var sw = Stopwatch.StartNew();
            var document = BuildInput(xml, limits, options);
            var parseMs = sw.ElapsedMilliseconds;

            sw.Restart();
            Run(document, listener, messages, errors, writer, budget);
            var transformMs = sw.ElapsedMilliseconds;

            sw.Restart();
            output.Flush();
            var serializeMs = sw.ElapsedMilliseconds;

            return new XsltTransformTimings(parseMs, CompileMs: 0, transformMs, serializeMs);
        }
        finally
        {
            probe?.Invoke(new SaxonTransformProbe(listener.MaxSeen, listener.EnterCount, messages.Count,
                messages.Messages, writer.CharsWritten, budget.Tripped));
        }
    }

    private XdmNode BuildInput(string xml, XsltExecutionLimits limits, SaxonEngineOptions options)
    {
        try
        {
            return _bridge.Build(xml, new TreeBridgeLimits(limits.MaxInputDepth, limits.MaxInputNodes, options.MaxInputCharacters));
        }
        catch (XmlException ex)
        {
            throw new XsltProcessingException(XsltDiagnostics.FromXmlException(ex, XsltFailureKind.Xml, XsltEngineKind.Saxon), ex);
        }
    }

    private void Run(XdmNode document, DepthLimitingTraceListener listener, BoundedMessageHandler messages,
        BoundedErrorReporter errors, LimitedJavaWriter writer, ExecutionBudget budget)
    {
        try
        {
            var transformer = _factory.CreateTransformer(_executable, listener, messages, errors);
            // applyTemplates global bağlam öğesini KURMAZ → <xsl:variable name="XML" select="/"/> XPDY0002 (spike tuzağı).
            transformer.setGlobalContextItem(document);
            transformer.applyTemplates(document, transformer.newSerializer(writer));
        }
        catch (XsltProcessingException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not InsufficientExecutionStackException)
        {
            throw new XsltProcessingException(SaxonDiagnostics.FromRuntime(ex, budget.Tripped), ex);
        }
    }
}
