using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;
using Xunit.Abstractions;

namespace XsltCraft.Tests.Eval;

/// <summary>
/// İsteğe bağlı kuru-çalıştırma: <c>XSLTCRAFT_CORPUS_DIR</c> ortam değişkeni verilirse o dizindeki
/// tüm <c>*.xslt</c> dosyalarını Store ve Execute modunda tarar, yalnız <b>sayım ve kod dağılımı</b>
/// raporlar (içerik basmaz). Değişken yoksa test atlanır (skip). CI'da bloke etmez.
/// </summary>
[Trait("Category", "Corpus")]
public class CorpusDryRunTests
{
    private readonly ITestOutputHelper _output;

    public CorpusDryRunTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Corpus_Scan_ReportsCountsOnly()
    {
        var dir = Environment.GetEnvironmentVariable("XSLTCRAFT_CORPUS_DIR");
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            // SkippableFact paketi yok; env tanımsızsa no-op (CI'da bloke etmez).
            _output.WriteLine("XSLTCRAFT_CORPUS_DIR tanımlı değil — corpus kuru-çalıştırma atlandı.");
            return;
        }

        var files = Directory.GetFiles(dir, "*.xslt", SearchOption.AllDirectories);
        _output.WriteLine($"Corpus: {files.Length} dosya — {dir}");

        var storeCodes = new Dictionary<string, int>();
        var executeCodes = new Dictionary<string, int>();
        int storeFlagged = 0, executeFlagged = 0;

        foreach (var file in files)
        {
            string xslt;
            try { xslt = File.ReadAllText(file); }
            catch { continue; }

            var store = XsltSafety.Scan(xslt, XsltSafetyMode.Store);
            if (store.HasFindings) storeFlagged++;
            foreach (var f in store.Findings)
                storeCodes[f.Code] = storeCodes.GetValueOrDefault(f.Code) + 1;

            var execute = XsltSafety.Scan(xslt, XsltSafetyMode.Execute);
            if (execute.HasFindings) executeFlagged++;
            foreach (var f in execute.Findings)
                executeCodes[f.Code] = executeCodes.GetValueOrDefault(f.Code) + 1;
        }

        _output.WriteLine($"Store: {storeFlagged}/{files.Length} dosyada bulgu");
        foreach (var (code, count) in storeCodes.OrderByDescending(kv => kv.Value))
            _output.WriteLine($"  [Store] {code}: {count}");

        _output.WriteLine($"Execute: {executeFlagged}/{files.Length} dosyada bulgu");
        foreach (var (code, count) in executeCodes.OrderByDescending(kv => kv.Value))
            _output.WriteLine($"  [Execute] {code}: {count}");
    }
}
