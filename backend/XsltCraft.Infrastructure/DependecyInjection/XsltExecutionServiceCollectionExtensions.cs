using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt;
using XsltCraft.Infrastructure.Xslt.InProcess;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.DependencyInjection;

/// <summary>
/// XSLT yürütme hattı (ADR 007/008): <c>Xslt</c> ayarları, kullanıcı kapısı, worker havuzu + adaptörü,
/// süreç içi korumalı host (<see cref="InProcessXsltHost"/> + <see cref="XsltExecutionGuard"/> + Saxon ısınması),
/// güvenlik tarayıcısı ve <see cref="IXsltExecutionService"/>.
/// <para><b>G1 kuralı:</b> <see cref="IXsltUserGate"/> havuzdan ÖNCE kaydedilir; havuz kapısız
/// erişilebilir hâle getirilmez (kullanıcı kaynaklı kill'ler global kesiciyi açabilir).</para>
/// </summary>
public static class XsltExecutionServiceCollectionExtensions
{
    public static IServiceCollection AddXsltExecution(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XsltExecutionOptions>()
            .Bind(configuration.GetSection(XsltExecutionOptions.SectionName))
            .Validate(o => o.Validate().Count == 0, $"{XsltExecutionOptions.SectionName} ayarları geçersiz.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // 1) Kullanıcı kapısı — havuzdan önce (G1).
        services.TryAddSingleton<IXsltUserGate, XsltUserGate>();

        // 2) İzole worker havuzu (hosted service; açılışı bekletmez, worker yoksa Warning + EngineUnavailable).
        services.AddXsltWorkerPool(configuration);
        services.TryAddSingleton<IXsltWorkerPool>(sp => new XsltWorkerPoolAdapter(sp.GetRequiredService<XsltWorkerPool>()));
        services.TryAddSingleton<IXsltIsolatedHost, WorkerXsltHost>();

        // 3) Süreç içi grid host (ADR 008 §7): guard + motorlar (Saxon tek Processor, tembel) + bozulmuş mod → worker.
        services.TryAddSingleton<XsltInProcessMetrics>();
        services.TryAddSingleton<IXsltGuardObserver>(sp => new XsltGuardObserver(
            sp.GetRequiredService<ILogger<XsltGuardObserver>>(),
            RestartCallback(sp)));
        services.TryAddSingleton(sp =>
        {
            var guard = new XsltExecutionGuard(
                sp.GetRequiredService<IOptions<XsltExecutionOptions>>().Value.Grid,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<IXsltGuardObserver>());
            sp.GetRequiredService<XsltInProcessMetrics>().Attach(guard);
            return guard;
        });
        services.TryAddSingleton(sp => InProcessXsltEngines.CreateDefault(
            sp.GetRequiredService<IOptions<XsltExecutionOptions>>().Value.Limits.Grid.MaxXsltChars));
        services.TryAddSingleton(sp => new InProcessXsltHost(
            sp.GetRequiredService<XsltExecutionGuard>(),
            sp.GetRequiredService<InProcessXsltEngines>(),
            sp.GetRequiredService<IXsltIsolatedHost>(),
            sp.GetRequiredService<ILogger<InProcessXsltHost>>(),
            sp.GetRequiredService<XsltInProcessMetrics>()));
        services.TryAddSingleton<IXsltInProcessHost>(sp => sp.GetRequiredService<InProcessXsltHost>());
        services.AddHostedService<SaxonWarmupService>();

        // 4) Tarayıcı + servis (Application IOptions almaz → POCO fabrikası).
        services.TryAddSingleton<IXsltSafetyScanner, DefaultXsltSafetyScanner>();
        services.TryAddSingleton<IXsltExecutionService>(sp => new XsltExecutionService(
            sp.GetRequiredService<IOptions<XsltExecutionOptions>>().Value,
            sp.GetRequiredService<IXsltSafetyScanner>(),
            sp.GetRequiredService<IXsltUserGate>(),
            sp.GetRequiredService<IXsltIsolatedHost>(),
            sp.GetRequiredService<IXsltInProcessHost>()));

        return services;
    }

    /// <summary>
    /// Takılı kaçakta yeniden başlatma: yalnız <c>Xslt:Grid:RestartOnStuckRunaway=true</c> VE host varsa
    /// (<see cref="IHostApplicationLifetime"/>; SAHİP KARARI varsayılan <c>false</c> → yalnız Critical log).
    /// </summary>
    private static Action? RestartCallback(IServiceProvider sp)
    {
        if (!sp.GetRequiredService<IOptions<XsltExecutionOptions>>().Value.Grid.RestartOnStuckRunaway)
            return null;

        var lifetime = sp.GetService<IHostApplicationLifetime>();
        return lifetime is null ? null : lifetime.StopApplication;
    }
}
