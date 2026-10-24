using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Süreç başına motor örnekleri: tek <see cref="SaxonXsltEngine"/> (<see cref="SaxonEngineOptions.Isolated"/>;
/// Processor singleton) + <see cref="DotNetXsltEngine"/>. Motor thread'inde oluşturulur.
/// </summary>
internal sealed class WorkerEngineSet
{
    public WorkerEngineSet(SaxonXsltEngine saxon, IXsltEngine dotNet)
    {
        Saxon = saxon ?? throw new ArgumentNullException(nameof(saxon));
        DotNet = dotNet ?? throw new ArgumentNullException(nameof(dotNet));
        SaxonVersion = saxon.SaxonVersion;
    }

    public SaxonXsltEngine Saxon { get; }

    public IXsltEngine DotNet { get; }

    /// <summary>Hello'da bildirilen sürüm (ör. "12.10").</summary>
    public string SaxonVersion { get; }

    public static WorkerEngineSet Create() =>
        new(new SaxonXsltEngine(SaxonEngineOptions.Isolated), new DotNetXsltEngine());

    /// <summary>Tel adı → motor; tanımsız ad → <c>false</c>.</summary>
    public bool TryResolve(string? wireName, out IXsltEngine engine)
    {
        engine = wireName switch
        {
            WorkerEngines.Saxon => Saxon,
            WorkerEngines.DotNet => DotNet,
            _ => null!,
        };
        return engine is not null;
    }
}
