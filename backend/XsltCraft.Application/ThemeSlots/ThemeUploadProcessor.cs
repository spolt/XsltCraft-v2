using System.Text.RegularExpressions;

using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots;

public abstract record ThemeUploadOutcome
{
    /// <summary>Admin karar vermeli (HTTP 409): önceden gömülü görsel bloğu varsa "kaldır"/"koru" (<paramref name="Inspection"/>),
    /// temada statik görseller varsa her biri için logo / imza / tasarım (<paramref name="Images"/>).</summary>
    public sealed record DecisionRequired(OverridesInspection? Inspection, IReadOnlyList<ThemeImageInfo> Images) : ThemeUploadOutcome;

    /// <summary>Yükleme reddedildi (HTTP 422): "koru" uygulanamıyor, analiz başarısızken metni değiştiren bir karar
    /// verildi ya da sunucunun dönüştürdüğü metin son denetimden (tarama + derleme) geçmedi.</summary>
    public sealed record Rejected(string Message) : ThemeUploadOutcome;

    /// <param name="Xslt">Saklanacak metin (işaretli tema). <paramref name="Original"/> true ise özgün baytlar saklanmalı.</param>
    public sealed record Ready(string Xslt, string CatalogJson, ThemeSlotCatalog Catalog, bool Original) : ThemeUploadOutcome;
}

/// <summary>
/// Admin tema yüklemesinin tek giriş noktası: önceden gömülü görsel bloğu kararını uygular, ardından temayı
/// analiz edip logo/imza yuvalarını işaretler. Controller yalnız bunu çağırır, sonucu saklar. Sunucunun
/// dönüştürdüğü metin saklanmadan önce son kez taranır ve derlenir; admin'in kendi dosyası olduğu gibi kabul edilir.
/// </summary>
public interface IThemeUploadProcessor
{
    /// <param name="imageRoles">Temadaki statik görseller için kararlar, 409 yanıtındaki sırayla. Görsel varken null → 409.</param>
    ThemeUploadOutcome Process(string xslt, DocumentType type, OverridesDecision? decision,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null);
}

public sealed partial class ThemeUploadProcessor(
    IThemeOverridesProcessor overrides, IThemeSlotAnalyzer analyzer, IXsltHtmlRenderer renderer, IThemeSampleXmlProvider samples)
    : IThemeUploadProcessor
{
    [GeneratedRegex(@"^\s*<\?xml[^>]*\bencoding\s*=\s*[""']([^""']+)[""']", RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredEncoding();

    public ThemeUploadOutcome Process(string xslt, DocumentType type, OverridesDecision? decision,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null)
    {
        if (xslt.Length > 0 && xslt[0] == '﻿')
            xslt = xslt[1..];
        var uploaded = xslt;

        // Önizlemede render edilemeyen tema yayımlanmaz — karar sorulmadan (409) ve analizden önce.
        if (RenderProblem(xslt, type) is { } renderProblem)
            return new ThemeUploadOutcome.Rejected(renderProblem);

        // Analiz metni UTF-8 olarak yeniden yazar; bildirimi farklı kodlama diyen dosyada bayt/bildirim uyuşmazlığı
        // doğmasın diye dokunulmaz.
        var encoding = DeclaredEncoding().Match(xslt);
        if (encoding.Success && !encoding.Groups[1].Value.Trim().Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            var skipped = new ThemeSlotCatalog(ThemeSlotCatalog.CurrentVersion, ThemeSlotCatalog.CurrentAnalyzerVersion,
                ThemeSlotStatus.Skipped, DateTime.UtcNow, [], [], [], null,
                [new ThemeSlotWarning("not-utf8", $"Tema {encoding.Groups[1].Value} kodlamalı; konum analizi yalnız UTF-8 temalarda yapılır.")]);
            return new ThemeUploadOutcome.Ready(xslt, ThemeSlotCatalogJson.Serialize(skipped), skipped, Original: true);
        }

        var inspection = overrides.Inspect(xslt);
        OverridesSummary? summary = null;
        IReadOnlyList<string> occupied = [];
        var warnings = new List<ThemeSlotWarning>();

        // Görseller bloksuz metinden: blok (betik/JSON) görsel sayılmaz, "koru" ile gömülenler de hariç tutulur —
        // böylece iki istekte de sıra aynıdır.
        var images = analyzer.DetectImages(OverridesBlockText.RemoveAll(xslt), type);
        var overridesPending = inspection.Present && decision is null;
        var imagesPending = images.Count > 0 && (imageRoles is null || imageRoles.Count != images.Count);
        if (overridesPending || imagesPending)
            return new ThemeUploadOutcome.DecisionRequired(inspection.Present ? inspection : null, images);

        if (inspection.Present)
        {
            if (decision == OverridesDecision.Keep && !inspection.Keepable)
                return new ThemeUploadOutcome.Rejected($"Görsel bloğu korunamıyor: {inspection.Reason}");

            var applied = overrides.Apply(xslt, decision.Value);
            if (!applied.Success)
                return new ThemeUploadOutcome.Rejected($"Görsel bloğu işlenemedi: {applied.Error}");

            xslt = applied.Xslt;
            occupied = applied.OccupiedHolderIds;
            summary = new OverridesSummary(true, decision == OverridesDecision.Keep ? "keep" : "strip",
                applied.ImagesKept, applied.BankRowsKept);
        }
        else if (inspection.DanglingMarkers > 0)
        {
            xslt = overrides.Apply(xslt, OverridesDecision.Strip).Xslt;
            warnings.Add(new ThemeSlotWarning("dangling-marker", "Eşleşmeyen görsel bloğu işaretleri temizlendi."));
        }

        var analysis = analyzer.Analyze(xslt, type, occupied, images.Count > 0 ? imageRoles : null);

        // Analiz başarısızsa yuva yok: "koru" ve görseli "temadan kaldır" konumsuz temada logo/imzayı geri dönüşsüz
        // gömer/siler → saklanmaz. Bloğu kaldırmak yuvalardan bağımsızdır; son kapıdan geçerse saklanır.
        var kept = inspection.Present && decision == OverridesDecision.Keep;
        var imagesStripped = images.Count > 0 && imageRoles?.Any(r => r.Strip) == true;
        if (analysis.Catalog.Status == ThemeSlotStatus.Failed && (kept || imagesStripped))
            return new ThemeUploadOutcome.Rejected(FailedWithDecisions(analysis.Catalog, kept, imagesStripped));

        if (!string.Equals(analysis.Xslt, uploaded, StringComparison.Ordinal) && StoredTextProblem(analysis.Xslt) is { } problem)
            return new ThemeUploadOutcome.Rejected(problem);

        var catalog = analysis.Catalog with
        {
            Overrides = summary,
            Warnings = [.. warnings, .. analysis.Catalog.Warnings],
        };
        return new ThemeUploadOutcome.Ready(analysis.Xslt, ThemeSlotCatalogJson.Serialize(catalog), catalog, Original: false);
    }

    // Başarısızlık uyarısı analizin son uyarısıdır.
    private static string FailedWithDecisions(ThemeSlotCatalog catalog, bool kept, bool imagesStripped)
    {
        var reason = catalog.Warnings.LastOrDefault()?.Message ?? "bilinmeyen hata";
        var hint = (imagesStripped, kept) switch
        {
            (true, true) => "Görselleri 'Görünsün', görsel bloğunu 'Kaldır' seçerek",
            (true, false) => "Görselleri 'Görünsün' seçerek",
            _ => "Görsel bloğunu 'Kaldır' seçerek",
        };
        return $"Tema analiz edilemedi ({reason}); seçtiğiniz görsel/blok kararları uygulanmadı. {hint} ya da temayı düzeltip yeniden deneyin.";
    }

    // Son kapı: sunucunun dönüştürdüğü metin (blok kararı, görsel kaldırma, işaretler) güvenli ve derlenebilir olmalı.
    private static string? StoredTextProblem(string xslt)
    {
        if (XsltSafety.FindThreat(xslt) is { } threat)
            return $"İşlenen tema güvenlik denetiminden geçmedi: {threat}";
        try
        {
            SecureXslt.Compile(xslt);
            return null;
        }
        catch (Exception ex)
        {
            return $"İşlenen tema derlenemedi: {ex.Message}";
        }
    }

    // Tema örnek belgeyle derlenip render edilebilmeli (önizleme ve analiz aynı motoru kullanır).
    private string? RenderProblem(string xslt, DocumentType type)
    {
        try
        {
            renderer.Render(xslt, samples.Get(type));
            return null;
        }
        catch (Exception ex)
        {
            var sample = type == DocumentType.Despatch ? "irsaliye" : "fatura";
            var hint = ex.Message.Contains("unknown XSLT function", StringComparison.OrdinalIgnoreCase)
                ? " Önizleme XSLT 1.0 motoruyla çalışır; XSLT 2.0'a özgü fonksiyonlar (ör. exists()) kullanılamaz."
                : string.Empty;
            return $"Tema örnek {sample} ile işlenemedi, yüklenmedi: {ex.Message}{hint}";
        }
    }
}
