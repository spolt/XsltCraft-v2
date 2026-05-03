using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Services;
using XsltCraft.Application.Validation;
using XsltCraft.Application.XPath;

using XsltCraft.Application.Ai;

using XsltCraft.Infrastructure.Ai;
using XsltCraft.Infrastructure.Auth;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Repositories;
using XsltCraft.Infrastructure.Services;
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
        else if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IStorageService, S3StorageService>();
        else
            throw new InvalidOperationException($"Desteklenmeyen storage provider: '{storageProvider}'. Geçerli değerler: Local, S3");

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
        services.AddScoped<IXsltGeneratorService, XsltGeneratorService>();
        services.AddSingleton<IUblTrBusinessRuleService, UblTrBusinessRuleService>();
        services.AddSingleton<IXPathEvaluator, XPathEvaluator>();

        // -------------------------------------------------
        // Repositories
        // -------------------------------------------------

        services.AddScoped<ITemplateRepository, FileTemplateRepository>();

        // -------------------------------------------------
        // User Management
        // -------------------------------------------------

        services.AddScoped<IUserActivityRecorder, UserActivityRecorder>();
        services.AddScoped<IUserManagementService, UserManagementService>();

        // -------------------------------------------------
        // AI Assistant
        // -------------------------------------------------

        services.AddMemoryCache();
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        var aiOptions = new AiOptions();
        configuration.GetSection(AiOptions.SectionName).Bind(aiOptions);

        services.AddHttpClient("ollama", client =>
        {
            client.BaseAddress = new Uri(aiOptions.Ollama.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddScoped<OllamaAssistantProvider>();
        services.AddScoped<IAiAssistantProvider>(sp => sp.GetRequiredService<OllamaAssistantProvider>());

        if (aiOptions.Gemini.Enabled)
        {
            services.AddHttpClient("gemini", client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            services.AddScoped<GeminiAssistantProvider>();
            services.AddScoped<IAiAssistantProvider>(sp => sp.GetRequiredService<GeminiAssistantProvider>());
        }

        services.AddScoped<AiProviderOrchestrator>();
        services.AddScoped<IAiFeatureFlagService, AiFeatureFlagService>();
        services.AddScoped<IAiProviderHealthService, AiProviderHealthService>();
        services.AddScoped<IAiTokenBudgetService, AiTokenBudgetService>();

        return services;
    }
}
