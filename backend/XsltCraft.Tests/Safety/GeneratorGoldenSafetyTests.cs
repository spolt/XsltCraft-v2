using System.Runtime.CompilerServices;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;
using Xunit.Abstractions;

namespace XsltCraft.Tests.Safety;

/// <summary>
/// Grid V2 generator'ının golden XSLT çıktıları (<c>__snapshots__/GridV2/Xslt.*.verified.txt</c>)
/// <see cref="XsltSafetyMode.Execute"/> modunda 0 bulgu vermeli — yani yasakçı tarayıcı generator'ın
/// meşru çıktısını (Validate adımını) kırmamalı.
/// </summary>
public class GeneratorGoldenSafetyTests
{
    private readonly ITestOutputHelper _output;

    public GeneratorGoldenSafetyTests(ITestOutputHelper output) => _output = output;

    private static string SnapshotDir([CallerFilePath] string? thisFile = null) =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, "__snapshots__", "GridV2");

    public static TheoryData<string> GoldenFiles
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory.GetFiles(SnapshotDir(), "Xslt.*.verified.txt"))
                data.Add(Path.GetFileName(path));
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(GoldenFiles))]
    public void GeneratorGolden_Execute_NoFindings(string fileName)
    {
        var xslt = File.ReadAllText(Path.Combine(SnapshotDir(), fileName));
        var report = XsltSafety.Scan(xslt, XsltSafetyMode.Execute);

        foreach (var f in report.Findings)
            _output.WriteLine($"{f.Severity}/{f.Code} @ {f.Line}:{f.Column} — {f.Message}");

        Assert.False(report.HasFindings, $"{fileName}: {report.Findings.Count} beklenmeyen bulgu");
    }
}
