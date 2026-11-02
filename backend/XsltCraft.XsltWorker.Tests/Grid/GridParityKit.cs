using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Grid;

/// <summary>
/// Grid motor paritesi (T3.3) ortak parçaları: T1.4 fixture'ları (XsltCraft.Tests'ten linkli), generator'ın
/// önizlemedeki gerçek çağrısı ve <see cref="IXsltExecutionService"/>'in gerçek DI kaydı (<c>AddXsltExecution</c>).
/// </summary>
internal static class GridParityKit
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>GridV2GoldenTests.Assets ile aynı (önizleme denetleyicisinin data URI eşlemesinin deterministik karşılığı).</summary>
    private static readonly Dictionary<string, string> Assets = new()
    {
        ["aaaaaaaa-0000-4000-8000-000000000001"] =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
    };

    public static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string SnapshotDir => Path.Combine(AppContext.BaseDirectory, "GridSnapshots");

    public static string FixtureJson(string fixture) => File.ReadAllText(FixturePath("GridV2", fixture + ".json"));

    public static string Xml(string name) => File.ReadAllText(FixturePath("Xml", name + ".xml"));

    /// <summary><c>PreviewController.Preview</c>'deki çağrı: <c>GenerateV2(tree, assetBase64)</c>.</summary>
    public static string GenerateGrid(string fixture)
    {
        var tree = JsonSerializer.Deserialize<BlockTreeV2Dto>(FixtureJson(fixture), WebJson)!;
        var (xslt, error) = new XsltGeneratorService().GenerateV2(tree, Assets);
        Assert.Null(error);
        return xslt!;
    }

    /// <summary>Gerçek DI yolu; <c>Xslt:Engine:Grid</c> verilen moda sabitlenir.</summary>
    public static ServiceProvider Services(XsltEngineMode gridMode, bool workerEnabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Xslt:Engine:Grid"] = gridMode.ToString(),
            ["XsltWorker:Enabled"] = workerEnabled ? "true" : "false",
            ["XsltWorker:PoolSize"] = "1",
            ["XsltWorker:StartupTimeoutMs"] = "20000",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXsltExecution(configuration);
        return services.BuildServiceProvider();
    }

    public static async Task<XsltRunResult> RenderAsync(IXsltExecutionService service, XsltRoute route, string xslt, string xml)
    {
        var result = await service.ExecuteAsync(new XsltRunRequest(route, xslt, xml, XsltOperation.Render, Guid.NewGuid()));
        Assert.True(result.Succeeded, $"{route}: {result.Diagnostic?.Kind} {result.Diagnostic?.ErrorCode} {result.Diagnostic?.Message}");
        return result;
    }

    public static string Normalize(string html) => SnapshotScrubber.ScrubDataUris(HtmlNormalizer.Normalize(html));

    /// <summary>
    /// <c>#qrvalue</c>'nun QR kütüphanesine giden yükü, UTF-8 bayt olarak; element yoksa <c>null</c>.
    /// Generator'ın betiği yükü <c>v.textContent.trim()</c> ile okur → yalnız baş/son boşluk atılır, içerideki her
    /// karakter (boşluk dahil) korunur. Baş/son boşluk motor serileştirmesidir: Saxon <c>indent="yes"</c> metin
    /// düğümünün kenar girintisini yeniden yazar, .NET şablondaki girintiyi aynen bırakır.
    /// </summary>
    public static byte[]? QrValueBytes(string html)
    {
        var element = new HtmlParser().ParseDocument(html).GetElementById("qrvalue");
        return element is null ? null : Encoding.UTF8.GetBytes(element.TextContent.Trim(JsWhitespace));
    }

    /// <summary>ECMAScript <c>String.prototype.trim</c>'in sık görülen boşlukları (WhiteSpace + LineTerminator alt kümesi).</summary>
    private static readonly char[] JsWhitespace = [' ', '\t', '\n', '\r', '\v', '\f', (char)0x00A0, (char)0xFEFF, (char)0x2028, (char)0x2029];

    /// <summary>Verify'in BOM + son satır sonu farkından bağımsız karşılaştırma biçimi.</summary>
    public static string Canonical(string text) => text.TrimStart((char)0xFEFF).Replace("\r\n", "\n").TrimEnd('\n');
}
