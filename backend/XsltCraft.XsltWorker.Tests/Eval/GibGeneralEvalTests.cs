using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Tests.Engine;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Eval;

/// <summary>
/// Eval case008 — GİB <c>general.xslt</c> (XSLT 2.0: <c>exists()</c>, <c>xsl:character-map</c>) Raw rotada gerçek
/// worker havuzu (Saxon) ile render edilir. Kullanıcının asıl sorunu: .NET motorunda
/// <c>'exists()' is an unknown XSLT function</c>. İnsan-okur: docs/ecc/evaluations/xslt/case008-xslt2-gib-general.md.
/// </summary>
[Trait("Category", "Eval")]
[Collection(WorkerProcessesCollection.Name)]
public sealed class GibGeneralEvalTests
{
    private const string InvoiceNumber = "TST2026000000001";
    private const string Ettn = "111d1111-0c11-01bb-123e-1f2f34b567f8";
    private const string ExportBranchLabel = "Karşı Belge No:";
    private const string ExportDocNumber = "KB-EVAL-0042";

    [Fact]
    public async Task Case008_GibGeneral_RawRoute_RendersOnSaxon_WithInvoiceNumberAndEttn()
    {
        await using var provider = await StartAsync();
        var xslt = provider.GetRequiredService<IXsltExecutionService>();

        var result = await Render(xslt, EngineTestKit.InvoiceXml);

        Assert.True(result.Succeeded, result.Diagnostic?.Message);
        Assert.Equal(XsltEngineKind.Saxon, result.Engine);
        Assert.Equal(XsltExecutionPlace.Isolated, result.Place);
        Assert.Contains(InvoiceNumber, result.Html);
        Assert.Contains(Ettn, result.Html);
        Assert.DoesNotContain(ExportBranchLabel, result.Html); // exists() dalı: koşul sağlanmıyor
    }

    [Fact]
    public async Task Case008_GibGeneral_ExistsBranchAndCharacterMap_EvaluatedAtRuntime()
    {
        await using var provider = await StartAsync();
        var xslt = provider.GetRequiredService<IXsltExecutionService>();

        var result = await Render(xslt, ExportInvoiceWithC1Char());

        Assert.True(result.Succeeded, result.Diagnostic?.Message);
        // exists(//cac:AdditionalDocumentReference/cbc:DocumentType[text()='KARSIBELGENO']) çalışma anında true.
        Assert.Contains(ExportBranchLabel, result.Html);
        Assert.Contains(ExportDocNumber, result.Html);
        // character-map "a": U+0080–U+009F boş dizeye eşlenir (Note içindeki U+0096 çıktıda yok).
        Assert.Contains("SemihPolat", result.Html);
        Assert.DoesNotContain('\u0096', result.Html!);
    }

    private static Task<XsltRunResult> Render(IXsltExecutionService xslt, string xml) =>
        xslt.ExecuteAsync(new XsltRunRequest(XsltRoute.Raw, EngineTestKit.GibGeneral, xml, XsltOperation.Render, Guid.NewGuid()));

    /// <summary>Örnek faturanın İHRACKAYITLI/702 + KARSIBELGENO varyantı; bir Note'a C1 kontrol karakteri eklenir.</summary>
    private static string ExportInvoiceWithC1Char()
    {
        var xml = EngineTestKit.InvoiceXml
            .Replace("<cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>", "<cbc:InvoiceTypeCode>IHRACKAYITLI</cbc:InvoiceTypeCode>")
            .Replace("Satış Temsilcisi: Semih Polat", "Satış Temsilcisi: Semih\u0096Polat");

        var signature = xml.IndexOf("<cac:Signature>", StringComparison.Ordinal);
        xml = xml.Insert(signature,
            $"<cac:AdditionalDocumentReference><cbc:ID>{ExportDocNumber}</cbc:ID><cbc:IssueDate>2026-01-01</cbc:IssueDate>" +
            "<cbc:DocumentType>KARSIBELGENO</cbc:DocumentType></cac:AdditionalDocumentReference>");

        var taxTotal = xml.IndexOf("<cac:TaxTotal>", StringComparison.Ordinal);
        var category = xml.IndexOf("<cac:TaxCategory>", taxTotal, StringComparison.Ordinal) + "<cac:TaxCategory>".Length;
        xml = xml.Insert(category, "<cbc:TaxExemptionReasonCode>702</cbc:TaxExemptionReasonCode>");

        Assert.NotEqual(EngineTestKit.InvoiceXml, xml);
        return xml;
    }

    private static async Task<ServiceProvider> StartAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XsltWorker:PoolSize"] = "1",
            ["XsltWorker:StartupTimeoutMs"] = "20000",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXsltExecution(configuration);
        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<XsltWorkerPool>();
        await pool.StartAsync(CancellationToken.None);
        Assert.True(await WorkerTestEnvironment.WaitUntilAsync(() => pool.ReadyCount >= 1, TimeSpan.FromSeconds(30)), "worker hazır değil");
        return provider;
    }
}
