using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Auth;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Preview;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Themes;
using XsltCraft.Application.Validation;
using XsltCraft.Application.XPath;
using XsltCraft.Application.Xslt;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;

using XsltCraft.Infrastructure.Ai;
using XsltCraft.Infrastructure.Auth;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Services;
using XsltCraft.Infrastructure.Storage;
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
        services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // -------------------------------------------------
        // XSLT Engine
        // -------------------------------------------------

        // XSLT yürütme hattı: kullanıcı kapısı (havuzdan önce) + worker havuzu + süreç içi host + IXsltExecutionService.
        services.AddXsltExecution(configuration);

        // -------------------------------------------------
        // Application Services
        // -------------------------------------------------

        services.AddScoped<IXsltGeneratorService, XsltGeneratorService>();
        services.AddSingleton<IUblTrBusinessRuleService, UblTrBusinessRuleService>();
        // Kendi guard'ı (grid slotlarından ayrı); fabrika: DI, grid XsltExecutionGuard'lı ctor'u seçmesin.
        services.AddSingleton<IXPathEvaluator>(_ => new XPathEvaluator());
        services.AddSingleton<IFixedNoteInjector, FixedNoteInjector>();
        services.AddSingleton<IXsltHtmlRenderer, XsltHtmlRenderer>();

        // Hazır tema logo/imza yuvaları: yüklemede analiz + kullanıcı görsellerinin tek kaynaktan eklenmesi.
        services.AddSingleton<IThemeSampleXmlProvider, ThemeSampleXmlProvider>();
        services.AddSingleton<IThemeImageValidator, ThemeImageValidator>();
        services.AddSingleton<IThemeOverridesProcessor, ThemeOverridesProcessor>();
        services.AddSingleton<IThemeSlotAnalyzer, ThemeSlotAnalyzer>();
        services.AddSingleton<IThemeUploadProcessor, ThemeUploadProcessor>();
        services.AddSingleton<IThemeSlotProjector, ThemeSlotProjector>();
        services.AddSingleton<IThemeAssetComposer, ThemeAssetComposer>();
        services.AddScoped<IThemeAccessGuard, ThemeAccessGuard>();

        // -------------------------------------------------
        // User Management
        // -------------------------------------------------

        services.AddScoped<IUserActivityRecorder, UserActivityRecorder>();
        services.AddScoped<IUserManagementService, UserManagementService>();

        // -------------------------------------------------
        // Membership / Entitlements / Quota
        // -------------------------------------------------

        services.Configure<MembershipOptions>(configuration.GetSection(MembershipOptions.SectionName));
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IUsageQuotaService, UsageQuotaService>();
        services.AddScoped<IUsageReportService, UsageReportService>();

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
        services.AddScoped<IAiFeedbackService, AiFeedbackService>();
        services.AddScoped<IAiExemplarService, AiExemplarService>();

        // Vision (ekran görüntüsü): Application servisleri IOptions almaz → POCO factory ile.
        services.AddSingleton<IAiImageValidator>(sp =>
            new AiImageValidator(sp.GetRequiredService<IOptions<AiOptions>>().Value.Vision));
        services.AddSingleton<IAiVisionThrottle, AiVisionThrottle>();
        services.AddScoped<IAiVisionAvailability, AiVisionRouter>();
        services.AddScoped<IAiVisionGate>(sp => new AiVisionGate(
            sp.GetRequiredService<IAiVisionAvailability>(),
            sp.GetRequiredService<IEntitlementService>(),
            sp.GetRequiredService<IAiVisionThrottle>(),
            sp.GetRequiredService<IAiImageValidator>(),
            sp.GetRequiredService<IOptions<AiOptions>>().Value.Vision));

        return services;
    }
}
