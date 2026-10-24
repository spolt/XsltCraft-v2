using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Süreç içi motorlar (singleton). .NET motoru hazır; Saxon (IKVM + <c>Processor</c>, ~80 MB RSS, soğuk ~0.7 s)
/// ilk kullanımda ya da <see cref="SaxonWarmupService"/> ile bir kez kurulur — örnek başına TEK Processor.
/// Kurulum hatası önbelleğe alınmaz (sonraki istek yeniden dener) ve <see cref="XsltFailureKind.EngineUnavailable"/> olur.
/// </summary>
public sealed class InProcessXsltEngines
{
    internal const string SaxonUnavailableCode = "InProcessSaxonUnavailable";

    private const string WarmupXslt =
        "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
        "<xsl:template match=\"/\"><p><xsl:value-of select=\"string-join(for $i in r/i return upper-case($i), ',')\"/></p></xsl:template>" +
        "</xsl:stylesheet>";

    private const string WarmupXml = "<r><i>a</i><i>b</i></r>";

    private readonly IXsltEngine _dotNet;
    private readonly Func<IXsltEngine> _saxonFactory;
    private readonly object _lock = new();
    private IXsltEngine? _saxon;

    public InProcessXsltEngines(IXsltEngine dotNet, Func<IXsltEngine> saxonFactory)
    {
        ArgumentNullException.ThrowIfNull(dotNet);
        ArgumentNullException.ThrowIfNull(saxonFactory);
        _dotNet = dotNet;
        _saxonFactory = saxonFactory;
    }

    /// <summary>Üretim kurulumu: .NET + Saxon <see cref="SaxonEngineOptions.InProcessGrid"/> (stylesheet karakter sınırı rotaya hizalı).</summary>
    public static InProcessXsltEngines CreateDefault(int maxStylesheetChars) => new(
        new DotNetXsltEngine(),
        () => new SaxonXsltEngine(SaxonEngineOptions.InProcessGrid with
        {
            MaxStylesheetCharacters = Math.Max(SaxonEngineOptions.InProcessGrid.MaxStylesheetCharacters, maxStylesheetChars),
        }));

    public bool IsSaxonLoaded => Volatile.Read(ref _saxon) is not null;

    public IXsltEngine Get(XsltEngineKind kind) => kind switch
    {
        XsltEngineKind.DotNet => _dotNet,
        XsltEngineKind.Saxon => Saxon(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Saxon'u kurar ve küçük bir 2.0 dönüşümle ısıtır; çıktıyı döner (doğrulama/test).</summary>
    public string WarmUpSaxon()
    {
        var engine = Saxon();
        var compiled = engine.Compile(WarmupXslt, XsltExecutionLimits.Default);
        using var output = new StringWriter();
        compiled.Transform(WarmupXml, output, XsltExecutionLimits.Default);
        return output.ToString();
    }

    private IXsltEngine Saxon()
    {
        var engine = Volatile.Read(ref _saxon);
        if (engine is not null)
            return engine;

        lock (_lock)
        {
            if (_saxon is not null)
                return _saxon;

            try
            {
                engine = _saxonFactory();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw new XsltProcessingException(
                    new XsltDiagnostic("Süreç içi XSLT 2.0 motoru yüklenemedi.", 0, 0,
                        XsltFailureKind.EngineUnavailable, XsltEngineKind.Saxon, SaxonUnavailableCode), ex);
            }

            Volatile.Write(ref _saxon, engine);
            return engine;
        }
    }
}
