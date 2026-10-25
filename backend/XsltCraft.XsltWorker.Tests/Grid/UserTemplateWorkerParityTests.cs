using Microsoft.Extensions.DependencyInjection;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Tests.Support;
using static XsltCraft.XsltWorker.Tests.Grid.GridParityKit;

namespace XsltCraft.XsltWorker.Tests.Grid;

/// <summary>
/// T3.3 — kayıtlı şablon önizlemesi (<see cref="XsltRoute.UserTemplate"/>, gerçek worker, Saxon, izole) ile canlı grid
/// önizlemesi (<see cref="XsltRoute.Grid"/>, süreç içi Saxon) aynı generator XSLT'sinde aynı normalize HTML'i vermeli.
/// XSLT, <c>PreviewUserTemplate</c>'teki gibi <c>GenerateFromJson</c> ile üretilir (iki yolda aynı metin).
/// </summary>
[Trait("Category", "WorkerIntegration")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class UserTemplateWorkerParityTests
{
    [Fact]
    public async Task UserTemplate_Worker_Equals_Grid_InProcess()
    {
        await using var provider = Services(XsltEngineMode.Saxon, workerEnabled: true);
        var pool = provider.GetRequiredService<XsltWorkerPool>();
        await pool.StartAsync(CancellationToken.None);
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => pool.ReadyCount >= 1, TimeSpan.FromSeconds(30)), "worker hazır değil");
        var service = provider.GetRequiredService<IXsltExecutionService>();

        var (xslt, error) = new XsltGeneratorService().GenerateFromJson(FixtureJson("invoice-ready-blocks"));
        Assert.Null(error);
        var xml = Xml("invoice-multivalue");

        var worker = await RenderAsync(service, XsltRoute.UserTemplate, xslt!, xml);
        var inProcess = await RenderAsync(service, XsltRoute.Grid, xslt!, xml);

        Assert.Equal((XsltEngineKind.Saxon, XsltExecutionPlace.Isolated), (worker.Engine!.Value, worker.Place!.Value));
        Assert.Equal((XsltEngineKind.Saxon, XsltExecutionPlace.InProcess), (inProcess.Engine!.Value, inProcess.Place!.Value));
        Assert.Equal(Normalize(inProcess.Html!), Normalize(worker.Html!));
        Assert.Equal(QrValueBytes(inProcess.Html!), QrValueBytes(worker.Html!));
    }
}
