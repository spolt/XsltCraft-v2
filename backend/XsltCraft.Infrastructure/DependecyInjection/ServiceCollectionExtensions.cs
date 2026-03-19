using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Services;

using XsltCraft.Infrastructure.Auth;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Repositories;
using XsltCraft.Infrastructure.Storage;
using XsltCraft.Infrastructure.Templates;
using XsltCraft.Infrastructure.Xslt;

namespace XsltCraft.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXsltCraft(this IServiceCollection services, IConfiguration configuration)
    {
        // -------------------------------------------------
        // Database
        // -------------------------------------------------

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        // -------------------------------------------------
        // Storage
        // -------------------------------------------------

        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        if (storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IStorageService, LocalStorageService>();
        else
            throw new InvalidOperationException($"Desteklenmeyen storage provider: '{storageProvider}'. Geçerli değerler: Local");

        // -------------------------------------------------
        // Auth
        // -------------------------------------------------

        services.AddScoped<IJwtService, JwtService>();

        // -------------------------------------------------
        // Template Discovery
        // -------------------------------------------------

        services.AddSingleton<ITemplateDiscovery, TemplateDiscoveryEngine>();
        services.AddSingleton<XsltTemplateAnalyzer>();
        services.AddSingleton<TemplateDependencyGraph>();
        services.AddSingleton<TemplateHotReloadService>();

        // -------------------------------------------------
        // XSLT Engine
        // -------------------------------------------------

        services.AddSingleton<XsltCompiler>();
        services.AddScoped<IXsltTemplateRenderer, XsltTemplateRenderer>();

        // -------------------------------------------------
        // Template Core
        // -------------------------------------------------

        services.AddSingleton<ITemplateCache, TemplateCache>();
        services.AddSingleton<TemplateRegistry>();
        services.AddSingleton<ITemplateRegistry, TemplateRegistry>();

        // -------------------------------------------------
        // Application Services
        // -------------------------------------------------

        services.AddScoped<RenderService>();
        services.AddScoped<ITemplateService, TemplateService>();

        // -------------------------------------------------
        // Repositories
        // -------------------------------------------------

        services.AddScoped<ITemplateRepository, FileTemplateRepository>();

        return services;
    }
}
