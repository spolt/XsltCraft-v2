using Xunit.Abstractions;

using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.Tests.ThemeSlots;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Application.Tests.EasyEdit;

/// <summary>
/// Yerel bir XSLT örneğindeki görsellerin ve belge banka satırlarının Kolay düzenle analizini satır satır yazar
/// (taşınabilirlik, yükleme kuralı, banka tespiti). <c>XSLTCRAFT_EASYEDIT_SAMPLE</c> tanımlı değilse hiçbir şey yapmaz.
/// Görsel verisi (base64) yazılmaz; IBAN'ın yalnız son 4 hanesi yazılır.
/// Çalıştırma: <c>dotnet test backend/XsltCraft.Application.Tests --filter "Category=Diagnostic" --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Diagnostic")]
public class EasyEditSampleDiagnostics(ITestOutputHelper output)
{
    private const string SampleVariable = "XSLTCRAFT_EASYEDIT_SAMPLE";

    [Fact]
    public async Task Sample_images_report()
    {
        var path = Environment.GetEnvironmentVariable(SampleVariable);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // SkippableFact paketi yok; değişken tanımsızsa no-op (CI'da bloke etmez).
            output.WriteLine($"{SampleVariable} tanımlı değil ya da dosya yok; teşhis atlandı.");
            return;
        }

        var validator = new ThemeImageValidator();
        var analyzer = new UploadedXsltAnalyzer(new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(validator),
            new ThemeSlotAnalyzer(ThemeFixtures.Execution, ThemeFixtures.Samples));
        var xslt = await File.ReadAllTextAsync(path);
        var analysis = await analyzer.AnalyzeAsync(xslt, null, Guid.NewGuid(), CancellationToken.None);

        output.WriteLine($"verdict={analysis.Verdict} reason={analysis.ReasonCode ?? "-"} type={analysis.DocumentType} images={analysis.Images.Count}");
        if (analysis.Previous is { } previous)
            output.WriteLine($"previous recognized={previous.Recognized} hide=[{string.Join(",", previous.Hide)}] problem={previous.Problem ?? "-"}");

        foreach (var image in analysis.Images)
        {
            var src = image.PreviewDataUrl;
            var check = src is null ? null : validator.Check(src);
            output.WriteLine(string.Join(" | ",
                $"#{image.Index}",
                $"line={image.Line}",
                $"role={image.Role}",
                $"source={image.Source}",
                $"mime={MimePrefix(src)}",
                $"magic={Magic(src)}",
                $"length={src?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}",
                $"validator={(check is null ? "-" : check.Valid ? "ok" : check.Error)}",
                $"movable={image.Movable}",
                $"blocker={image.StaticBlocker ?? "-"}",
                $"slot={image.SlotId ?? "-"}"));
        }

        // Analiz yalnız kullanılabilir kararda banka bilgisi döndürür; değilse tespit doğrudan çalıştırılır.
        var bank = analysis.DocumentBank ?? DocumentBankDetector.Detect(xslt, analysis.DocumentType).ToInfo();
        output.WriteLine($"documentBank source={(analysis.DocumentBank is null ? "detector" : "analysis")} found={bank.Found} canAdd={bank.CanAdd} rows={bank.Rows.Count}");
        foreach (var row in bank.Rows)
        {
            output.WriteLine(string.Join(" | ",
                $"  bank#{row.Index}",
                $"line={row.Line}",
                $"name=\"{row.BankName}\"",
                $"iban=****{MaskedIban(row.Iban)}",
                $"nameEditable={row.BankNameEditable}",
                $"deletable={row.Deletable}"));
        }

        // Bellek içi ekleme denemesi; dosyaya yazılmaz.
        if (bank.CanAdd)
        {
            var added = DocumentBankEditor.Apply(xslt, analysis.DocumentType,
                [new DocumentBankEditInput(null, "ÖRNEK BANKA", "TR000000000000000000000000", false)]);
            output.WriteLine($"documentBank addCheck changed={added.Changed} rowsAfter={DocumentBankDetector.Detect(added.Xslt, analysis.DocumentType).Rows.Count} warnings=[{string.Join(",", added.Warnings.Select(w => w.Code))}]");
        }
    }

    // IBAN'ın yalnız son 4 hanesi.
    private static string MaskedIban(string iban)
    {
        var digits = iban.Where(char.IsAsciiDigit).ToArray();
        return digits.Length >= 4 ? new string(digits[^4..]) : "";
    }

    // Yalnız "data:image/...;base64" öneki; veri yazılmaz.
    private static string MimePrefix(string? src) =>
        src is null ? "-" : src.IndexOf(',') is var comma and > 0 ? src[..comma] : "?";

    // Base64 verisinin ilk karakterlerinden içerik türü tahmini (tür/içerik uyuşmazlığı için).
    private static string Magic(string? src)
    {
        if (src is null || src.IndexOf(',') is not (var comma and > 0))
            return "-";
        var data = src.AsSpan(comma + 1);
        return data switch
        {
            _ when data.StartsWith("iVBORw0KGgo") => "png",
            _ when data.StartsWith("/9j/") => "jpeg",
            _ when data.StartsWith("R0lGOD") => "gif",
            _ when data.StartsWith("UklGR") => "webp",
            _ when data.StartsWith("Qk") => "bmp",
            _ => "unknown",
        };
    }
}
