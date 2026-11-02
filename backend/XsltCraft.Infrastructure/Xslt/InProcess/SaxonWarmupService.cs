using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Açılışta süreç içi Saxon'u (IKVM + Processor) küçük bir 2.0 dönüşümle ısıtır — ilk grid isteği ~0.7 s soğuk
/// başlangıcı ödemesin. Api açılışını BEKLETMEZ (arka plan, uzun süreli thread); hata → Warning (istek yolu
/// ilk kullanımda yeniden dener). <c>Xslt:Engine:Grid = DotNet</c> iken süreç içi Saxon hiç kullanılmaz →
/// ısınma atlanır (~80 MB RSS boşuna harcanmaz).
/// </summary>
public sealed class SaxonWarmupService : IHostedService
{
    private readonly InProcessXsltEngines _engines;
    private readonly XsltExecutionOptions _options;
    private readonly ILogger<SaxonWarmupService> _logger;

    public SaxonWarmupService(InProcessXsltEngines engines, IOptions<XsltExecutionOptions> options, ILogger<SaxonWarmupService> logger)
    {
        _engines = engines;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Isınma görevi (başlatılmadıysa/atlandıysa tamamlanmış görev). Testler bekler.</summary>
    internal Task Completion { get; private set; } = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Engine.Grid == XsltEngineMode.DotNet)
        {
            _logger.LogInformation("Saxon ısınması atlandı: Xslt:Engine:Grid=DotNet (süreç içi Saxon kullanılmıyor).");
            return Task.CompletedTask;
        }

        Completion = Task.Factory.StartNew(WarmUp, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void WarmUp()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var output = _engines.WarmUpSaxon();
            if (!output.Contains("A,B", StringComparison.Ordinal))
            {
                _logger.LogWarning("Saxon ısınması beklenmeyen çıktı üretti ({DurationMs}ms).", sw.ElapsedMilliseconds);
                return;
            }

            _logger.LogInformation("Süreç içi Saxon ısındı ({DurationMs}ms).", sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Süreç içi Saxon ısınması başarısız ({DurationMs}ms); ilk istekte yeniden denenecek.", sw.ElapsedMilliseconds);
        }
    }
}
