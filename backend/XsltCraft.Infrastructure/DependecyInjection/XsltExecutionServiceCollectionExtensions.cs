using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.DependencyInjection;

/// <summary>
/// XSLT yürütme hattı (ADR 007/008): <c>Xslt</c> ayarları, kullanıcı kapısı, worker havuzu + adaptörü,
/// süreç içi host, güvenlik tarayıcısı ve <see cref="IXsltExecutionService"/>.
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

        // 3) Süreç içi grid host — GEÇİCİ .NET uygulaması; T2.4b InProcessXsltHost ile değiştirir.
        services.TryAddSingleton<IXsltInProcessHost, DotNetInProcessXsltHost>();

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
}
