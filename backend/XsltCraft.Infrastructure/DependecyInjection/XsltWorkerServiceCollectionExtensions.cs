using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.DependencyInjection;

/// <summary>
/// İzole XSLT worker havuzu kaydı. W1'de <c>AddXsltCraft</c>'a BAĞLI DEĞİL; W2'de
/// <c>WorkerXsltHost : IXsltIsolatedHost</c> adaptörüyle birlikte bağlanacak.
/// </summary>
public static class XsltWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddXsltWorkerPool(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XsltWorkerOptions>()
            .Bind(configuration.GetSection(XsltWorkerOptions.SectionName))
            .Validate(o => o.Validate().Count == 0, $"{XsltWorkerOptions.SectionName} ayarları geçersiz.")
            .ValidateOnStart();

        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<XsltWorkerMetrics>();
        services.TryAddSingleton<XsltWorkerLocator>();
        services.TryAddSingleton<IWorkerProcessStartInfoFactory, WorkerProcessStartInfoFactory>();
        services.TryAddSingleton<XsltWorkerPool>();
        services.AddHostedService(sp => sp.GetRequiredService<XsltWorkerPool>());

        return services;
    }
}
