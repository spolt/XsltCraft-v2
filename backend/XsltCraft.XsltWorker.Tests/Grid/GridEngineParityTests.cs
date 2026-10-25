using Microsoft.Extensions.DependencyInjection;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using static XsltCraft.XsltWorker.Tests.Grid.GridParityKit;

namespace XsltCraft.XsltWorker.Tests.Grid;

/// <summary>Grid=DotNet ve Grid=Saxon için iki gerçek <see cref="IXsltExecutionService"/> (worker kapalı; grid süreç içi).</summary>
public sealed class GridEnginesFixture : IDisposable
{
    private readonly ServiceProvider _dotNet = Services(XsltEngineMode.DotNet, workerEnabled: false);
    private readonly ServiceProvider _saxon = Services(XsltEngineMode.Saxon, workerEnabled: false);

    public IXsltExecutionService DotNet => _dotNet.GetRequiredService<IXsltExecutionService>();

    public IXsltExecutionService Saxon => _saxon.GetRequiredService<IXsltExecutionService>();

    public void Dispose()
    {
        _dotNet.Dispose();
        _saxon.Dispose();
    }
}

/// <summary>
/// T3.3 — BLOKE EDEN grid motor paritesi (Grid=Auto'nun ön koşulu, ADR 007 A5).
/// Her golden render vakası (T1.4 <c>__snapshots__/GridV2/Html.{fixture}.{xml}.verified.txt</c> — vaka listesi
/// snapshot dosyalarından türetilir, golden'a eklenen her vaka otomatik pariteye girer) için generator XSLT'si
/// önizlemenin gerçek yolundan (<c>GenerateV2</c> → <see cref="XsltRoute.Grid"/> → <c>InProcessXsltHost</c>) hem .NET
/// hem Saxon ile render edilir:
/// <list type="bullet">
/// <item>normalize HTML iki motorda eşit VE .NET golden snapshot'ına eşit (Saxon birincil golden: ayrı
///   <c>.saxon.verified</c> dosyası yerine aynı snapshot'a bağlanır — parite gereği içerik zaten aynı olmalı);</item>
/// <item><c>#qrvalue</c> metni normalize edilmeden bayt-aynı (GİB karekod yükü).</item>
/// </list>
/// </summary>
public sealed class GridEngineParityTests(GridEnginesFixture engines) : IClassFixture<GridEnginesFixture>
{
    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var file in Directory.GetFiles(SnapshotDir, "Html.*.verified.txt").Order(StringComparer.Ordinal))
            {
                var parts = Path.GetFileName(file).Split('.');
                data.Add(parts[1], parts[2]);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GridRender_DotNetAndSaxon_NormalizedHtmlEqualAndMatchesGolden(string fixture, string xml)
    {
        var xslt = GenerateGrid(fixture);
        var input = Xml(xml);

        var dotNet = await RenderAsync(engines.DotNet, XsltRoute.Grid, xslt, input);
        var saxon = await RenderAsync(engines.Saxon, XsltRoute.Grid, xslt, input);

        Assert.Equal(XsltEngineKind.DotNet, dotNet.Engine);
        Assert.Equal(XsltEngineKind.Saxon, saxon.Engine);
        Assert.Equal(XsltExecutionPlace.InProcess, saxon.Place);

        var saxonHtml = Normalize(saxon.Html!);
        Assert.Equal(Normalize(dotNet.Html!), saxonHtml);

        var golden = File.ReadAllText(Path.Combine(SnapshotDir, $"Html.{fixture}.{xml}.verified.txt"));
        Assert.Equal(Canonical(golden), Canonical(saxonHtml));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GridRender_DotNetAndSaxon_QrValueByteIdentical(string fixture, string xml)
    {
        var xslt = GenerateGrid(fixture);
        var input = Xml(xml);

        var dotNet = QrValueBytes((await RenderAsync(engines.DotNet, XsltRoute.Grid, xslt, input)).Html!);
        var saxon = QrValueBytes((await RenderAsync(engines.Saxon, XsltRoute.Grid, xslt, input)).Html!);

        Assert.Equal(dotNet is null, saxon is null);
        if (dotNet is not null)
        {
            Assert.NotEmpty(dotNet);
            Assert.Equal(dotNet, saxon!);
        }
    }

    [Fact]
    public void Cases_CoverRequiredXmls_AndQrFixtures()
    {
        var rows = Cases.Select(r => ((string)r[0], (string)r[1])).ToList();

        Assert.NotEmpty(rows);
        foreach (var required in new[] { "invoice-multivalue", "invoice-rounding", "invoice-empty-numeric", "invoice-basic", "despatch-basic" })
            Assert.Contains(rows, r => r.Item2 == required);
        // QR içeren hazır blok fixture'ları çoklu-değer ve yuvarlama XML'leriyle pariteye girmeli.
        Assert.Contains(("invoice-ready-blocks", "invoice-multivalue"), rows);
        Assert.Contains(("invoice-ready-blocks", "invoice-rounding"), rows);
    }
}
