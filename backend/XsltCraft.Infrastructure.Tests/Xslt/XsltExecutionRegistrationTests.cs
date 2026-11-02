using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Xslt;
using XsltCraft.Infrastructure.Xslt.InProcess;
using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.Infrastructure.Tests.Xslt;

public sealed class XsltExecutionRegistrationTests
{
    private static ServiceCollection Services(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXsltExecution(configuration);
        return services;
    }

    [Fact]
    public void UserGate_IsRegisteredBeforePool()
    {
        var services = Services();

        var gate = services.ToList().FindIndex(d => d.ServiceType == typeof(IXsltUserGate));
        var pool = services.ToList().FindIndex(d => d.ServiceType == typeof(XsltWorkerPool));

        Assert.True(gate >= 0 && pool >= 0);
        Assert.True(gate < pool, "IXsltUserGate havuzdan önce kaydedilmeli (G1).");
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public async Task Resolves_Pipeline_WithoutStartingWorkers()
    {
        await using var provider = Services().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.IsType<XsltExecutionService>(provider.GetRequiredService<IXsltExecutionService>());
        Assert.IsType<XsltUserGate>(provider.GetRequiredService<IXsltUserGate>());
        Assert.IsType<WorkerXsltHost>(provider.GetRequiredService<IXsltIsolatedHost>());
        Assert.IsType<InProcessXsltHost>(provider.GetRequiredService<IXsltInProcessHost>());
        Assert.IsType<DefaultXsltSafetyScanner>(provider.GetRequiredService<IXsltSafetyScanner>());
        Assert.Same(provider.GetRequiredService<IXsltUserGate>(), provider.GetRequiredService<IXsltUserGate>());
        Assert.Equal(0, provider.GetRequiredService<XsltWorkerPool>().ReadyCount);
    }

    [Fact]
    public void Configuration_Binds()
    {
        using var provider = Services(new()
        {
            ["Xslt:Engine:Raw"] = "Saxon",
            ["Xslt:Engine:Grid"] = "Auto",
            ["Xslt:Limits:Raw:TimeoutMs"] = "4000",
            ["Xslt:UserGate:UserTemplateMaxConcurrent"] = "2",
            ["Xslt:Grid:MaxConcurrent"] = "3",
        }).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XsltExecutionOptions>>().Value;

        Assert.Equal(XsltEngineMode.Saxon, options.Engine.Raw);
        Assert.Equal(XsltEngineMode.Auto, options.Engine.Grid);
        Assert.Equal(4000, options.Limits.Raw.TimeoutMs);
        Assert.Equal(5_000_000, options.Limits.Raw.MaxXsltChars); // bağlanmayan alan varsayılanında kalır
        Assert.Equal(2, options.UserGate.UserTemplateMaxConcurrent);
        Assert.Equal(3, options.Grid.MaxConcurrent);
        Assert.False(options.Grid.RestartOnStuckRunaway);
    }

    [Fact]
    public void InvalidConfiguration_FailsValidation()
    {
        using var provider = Services(new() { ["Xslt:Limits:Raw:MaxXmlBytes"] = "20000000" }).BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<XsltExecutionOptions>>().Value);
    }

    [Fact]
    public async Task WorkerDisabled_V1RawRunsInProcess_V2Unavailable()
    {
        await using var provider = Services(new() { ["XsltWorker:Enabled"] = "false" }).BuildServiceProvider();
        var service = provider.GetRequiredService<IXsltExecutionService>();
        const string ns = "http://www.w3.org/1999/XSL/Transform";
        var user = Guid.NewGuid();

        var v1 = await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Raw,
            $"<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"{ns}\"><xsl:template match=\"/\"><b><xsl:value-of select=\"/r\"/></b></xsl:template></xsl:stylesheet>",
            "<r>merhaba</r>", XsltOperation.Render, user));
        var v2 = await service.ExecuteAsync(new XsltRunRequest(XsltRoute.Raw,
            $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{ns}\"><xsl:template match=\"/\"/></xsl:stylesheet>",
            "<r/>", XsltOperation.Render, user));

        Assert.True(v1.Succeeded, v1.Diagnostic?.Message);
        Assert.Contains("<b>merhaba</b>", v1.Html);
        Assert.Equal(XsltExecutionPlace.InProcess, v1.Place);
        Assert.Equal(XsltFailureKind.EngineUnavailable, v2.Diagnostic!.Kind);
    }
}
