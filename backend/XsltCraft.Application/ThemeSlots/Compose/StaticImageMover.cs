using System.Text;
using System.Text.RegularExpressions;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Compose;

/// <summary>Dosyanın kendi görseli için statik düzenleme.</summary>
/// <param name="Index">Analizdeki görsel sırası (<see cref="IThemeSlotAnalyzer.DetectImages"/>).</param>
/// <param name="Slot">Hedef yuva; null → yerinde kalır.</param>
/// <param name="Remove">Görsel metinden silinir.</param>
/// <param name="Alignment">null → değişmez.</param>
public sealed record StaticImageInput(int Index, string? Slot, bool Remove, int? Width, int? Height, ThemeAlignment? Alignment);

/// <param name="AddedBytes">Yuva grubundaki ek görsellerle metne eklenen görsel kaynağı (karakter).</param>
/// <param name="FilledBoxSlots">İçine statik görsel yerleşen kutu yuvaları; aynı birleştirmedeki yeni görsel bunlara yerleşmez.</param>
internal sealed record StaticMoveResult(string Xslt, IReadOnlyList<ThemeComposeWarning> Warnings, int AddedBytes, bool Changed,
    IReadOnlyCollection<string> FilledBoxSlots);

/// <summary>
/// Dosyadaki logo/imza görsellerini XSLT metninde statik olarak taşır ya da siler (betik bağımlılığı yok).
/// Görseller <see cref="IThemeSlotAnalyzer.DetectImages"/> sırasıyla eşlenir; yerleşim <c>ThemeAssetRuntime.js</c>'teki
/// <c>place()</c> ile aynıdır: hedef yuva bir tema görseliyse onun yerini alır (kutu korunur), boş kutuysa içine hizalı
/// bir sarmalayıcıyla eklenir; eklenen işaret kutusunun yerini sarmalayıcı alır ve yuva işaretini taşır. Taşınan ya da
/// silinen görselin eski yeri (kendi yuvası) boş eklenmiş işaret kutusu olarak kalır; başka görsel oraya taşınabilir.
/// Hedef yuva yoksa görsel yerinde kalır. Taşınan görseller <c>data-xc-placeholder</c> işaretini taşır; yeniden analizde rol işaretten okunur. Görsel bloğuna
/// dokunulmaz. Okunamayan durumda metin değişmeden döner.
/// </summary>
internal static class StaticImageMover
{
    /// <param name="ignoredKinds">Kullanıcının yeni görsel eklediği (tema görselini gizleyen) türler; bu türdeki istekler
    /// uyarıyla yok sayılır.</param>
    public static StaticMoveResult Apply(string xslt, DocumentType type, IReadOnlyList<StaticImageInput>? requests,
        IReadOnlySet<ThemeAssetKind> ignoredKinds)
    {
        if (requests is null || requests.Count == 0)
            return new StaticMoveResult(xslt, [], 0, false, []);

        var warnings = new StaticImageWarnings();
        try
        {
            if (PriorImageRoleReader.Locate(xslt, type) is not { } located)
            {
                foreach (var r in requests)
                    warnings.Add(StaticImageWarnings.NotFound, r.Index, null);
                return new StaticMoveResult(xslt, warnings.ToList(), 0, false, []);
            }

            // Yuva grubundaki ek görseller metni en çok istek sınırına kadar büyütebilir.
            var budget = (long)ThemeComposeRequestValidator.MaxXsltBytes - Encoding.UTF8.GetByteCount(xslt);
            var plan = new StaticImagePlan(located.Marked, OverridesBlockText.Scan(xslt).Blocks, located.Elements, ignoredKinds, warnings,
                budget);
            foreach (var r in plan.InApplyOrder(requests))
                plan.Apply(r);
            return plan.Changed
                ? new StaticMoveResult(plan.Render(), warnings.ToList(), plan.AddedBytes, true, plan.FilledBoxSlots)
                : new StaticMoveResult(xslt, warnings.ToList(), 0, false, []);
        }
        catch (Exception ex) when (ex is RegexMatchTimeoutException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return new StaticMoveResult(xslt, [StaticImageWarnings.SkippedWarning()], 0, false, []);
        }
    }
}
