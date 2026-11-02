using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;
using Xunit.Abstractions;

namespace XsltCraft.Application.Tests.ThemeSlots;

/// <summary>
/// Gerçek GİB/firma şablonları yasakçı tarayıcıda yanlış-pozitif üretmemeli:
/// <see cref="XsltSafetyMode.Store"/> modunda <b>0 Security</b> bulgusu. Execute modundaki bulgular
/// (çoğunlukla desteklenmeyen 3.0 ad alanları / fn'ler) bilgilendirme amacıyla test çıktısına yazılır
/// — çalıştırma hattının bunları neden engelleyebileceğini görünür kılar.
/// </summary>
public class XsltSafetyFixtureTests
{
    private readonly ITestOutputHelper _output;

    public XsltSafetyFixtureTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<string> FileFixtures => new(
        ThemeFixtures.GibGeneral,
        ThemeFixtures.GibEInvoice,
        ThemeFixtures.GibEIrsaliye,
        ThemeFixtures.GibTemplate,
        ThemeFixtures.TestTheme);

    [Theory]
    [MemberData(nameof(FileFixtures))]
    public void Fixture_Store_NoSecurityFindings(string fixture)
    {
        AssertStoreClean(fixture, ThemeFixtures.Load(fixture));
    }

    [Fact]
    public void CompanyImagesTheme_Store_NoSecurityFindings()
    {
        AssertStoreClean("CompanyImagesTheme", ThemeFixtures.CompanyImagesTheme);
    }

    private void AssertStoreClean(string name, string xslt)
    {
        var store = XsltSafety.Scan(xslt, XsltSafetyMode.Store);
        foreach (var f in store.Findings)
            _output.WriteLine($"[Store] {name}: {f.Code} @ {f.Line}:{f.Column} — {f.Message}");

        // Execute bulgularını raporla (engel değil, bilgi).
        var execute = XsltSafety.Scan(xslt, XsltSafetyMode.Execute);
        foreach (var f in execute.Findings)
            _output.WriteLine($"[Execute] {name}: {f.Severity}/{f.Code} @ {f.Line}:{f.Column} — {f.Message}");

        Assert.False(store.HasFindings, $"{name}: Store modunda {store.Findings.Count} Security bulgusu (0 beklenir)");
    }
}
