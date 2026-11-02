using System.Diagnostics;
using net.sf.saxon.s9api;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Diagnostics;
using XsltCraft.Xslt.Saxon.Input;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon;

/// <summary>
/// Saxon-HE 12.10 (XSLT 2.0 tavanı) motoru. Sandbox <see cref="SaxonProcessorFactory"/>'den gelir; örnek
/// başına tek <see cref="Processor"/> (thread-safe), derleme başına yeni derleyici. Hatalar yalnız
/// <see cref="XsltProcessingException"/>. Güvenlik taraması (XsltSafety) çağıranın işidir — bu sınıf
/// tarayıcıdan bağımsız olarak da güvenli olmalıdır (sandbox derinlemesine savunma değil, sınırdır).
/// Derleme anı özyinelemesine karşı büyük yığınlı thread çağıranın sorumluluğundadır (worker EngineThread).
/// </summary>
public sealed class SaxonXsltEngine : IXsltEngine
{
    private readonly SaxonProcessorFactory _factory;
    private readonly Processor _processor;

    public SaxonXsltEngine(SaxonEngineOptions? options = null)
    {
        _factory = new SaxonProcessorFactory(options ?? SaxonEngineOptions.Isolated);
        _processor = _factory.CreateProcessor();
    }

    public XsltEngineKind Kind => XsltEngineKind.Saxon;

    public SaxonEngineOptions Options => _factory.Options;

    /// <summary>Örn. "12.10" (Hello mesajı / ısınma için).</summary>
    public string SaxonVersion => _processor.getSaxonProductVersion();

    public ICompiledXslt Compile(string xslt, XsltExecutionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(xslt);
        ArgumentNullException.ThrowIfNull(limits);

        var sw = Stopwatch.StartNew();
        StylesheetSource.PreRead(xslt, limits.MaxStylesheetDepth, Options.MaxStylesheetCharacters);

        var errors = new BoundedErrorReporter(Options.MaxRetainedErrors, Options.MaxMessageChars);
        try
        {
            var compiler = _factory.CreateCompiler(_processor, errors);
            var executable = compiler.compile(StylesheetSource.Create(xslt));
            return new SaxonCompiledXslt(executable, _processor, _factory, sw.ElapsedMilliseconds);
        }
        catch (XsltProcessingException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not InsufficientExecutionStackException)
        {
            throw new XsltProcessingException(SaxonDiagnostics.FromCompile(ex, errors.Errors), ex);
        }
    }
}
