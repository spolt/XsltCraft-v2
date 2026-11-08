using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
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
/// Render/derleme tema hattından (<see cref="ThemeXsltRuns"/>): XSLT 2.0 yapısı kullanan tema (ör. GİB general.xslt)
/// Saxon'la izole worker'da, diğerleri .NET'le süreç içinde.
/// </summary>
public interface IThemeUploadProcessor
{
    /// <param name="imageRoles">Temadaki statik görseller için kararlar, 409 yanıtındaki sırayla. Görsel varken null → 409.</param>
    /// <param name="userId">Yükleyen admin (yürütme kapısı bölümü).</param>
    Task<ThemeUploadOutcome> ProcessAsync(string xslt, DocumentType type, OverridesDecision? decision,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null, Guid userId = default, CancellationToken cancellationToken = default);
}

public sealed class ThemeUploadProcessor(
    IThemeOverridesProcessor overrides, IThemeSlotAnalyzer analyzer, IXsltExecutionService execution, IThemeSampleXmlProvider samples)
    : IThemeUploadProcessor
{
    private const string EngineUnavailableMessage = "XSLT motoru şu anda kullanılamıyor; tema yüklenmedi, lütfen birazdan tekrar deneyin.";

    public async Task<ThemeUploadOutcome> ProcessAsync(string xslt, DocumentType type, OverridesDecision? decision,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null, Guid userId = default, CancellationToken cancellationToken = default)
    {
        xslt = ThemeTextPrelude.StripBom(xslt);
        var uploaded = xslt;

        // Önizlemede render edilemeyen tema yayımlanmaz — karar sorulmadan (409) ve analizden önce.
        if (await RenderProblemAsync(xslt, type, userId, cancellationToken).ConfigureAwait(false) is { } renderProblem)
            return new ThemeUploadOutcome.Rejected(renderProblem);

        // Analiz metni UTF-8 olarak yeniden yazar; bildirimi farklı kodlama diyen dosyada bayt/bildirim uyuşmazlığı
        // doğmasın diye dokunulmaz.
        if (ThemeTextPrelude.NonUtf8DeclaredEncoding(xslt) is { } encoding)
        {
            var skipped = new ThemeSlotCatalog(ThemeSlotCatalog.CurrentVersion, ThemeSlotCatalog.CurrentAnalyzerVersion,
                ThemeSlotStatus.Skipped, DateTime.UtcNow, [], [], [], null,
                [new ThemeSlotWarning("not-utf8", $"Tema {encoding} kodlamalı; konum analizi yalnız UTF-8 temalarda yapılır.")]);
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

        var analysis = await analyzer.AnalyzeAsync(xslt, type, occupied, images.Count > 0 ? imageRoles : null, userId,
            route: XsltRoute.Theme, cancellationToken)
            .ConfigureAwait(false);

        // Motor geçici olarak yoksa konumsuz tema saklanmaz (tema hatası değil — yeniden denenebilir).
        if (analysis.Catalog.Status == ThemeSlotStatus.Failed
            && analysis.Catalog.Warnings.LastOrDefault()?.Code == ThemeSlotAnalyzer.EngineUnavailableCode)
            return new ThemeUploadOutcome.Rejected($"{EngineUnavailableMessage} ({analysis.Catalog.Warnings[^1].Message})");

        // Analiz başarısızsa yuva yok: "koru" ve görseli "temadan kaldır" konumsuz temada logo/imzayı geri dönüşsüz
        // gömer/siler → saklanmaz. Bloğu kaldırmak yuvalardan bağımsızdır; son kapıdan geçerse saklanır.
        var kept = inspection.Present && decision == OverridesDecision.Keep;
        var imagesStripped = images.Count > 0 && imageRoles?.Any(r => r.Strip) == true;
        if (analysis.Catalog.Status == ThemeSlotStatus.Failed && (kept || imagesStripped))
            return new ThemeUploadOutcome.Rejected(FailedWithDecisions(analysis.Catalog, kept, imagesStripped));

        if (!string.Equals(analysis.Xslt, uploaded, StringComparison.Ordinal)
            && await StoredTextProblemAsync(analysis.Xslt, userId, cancellationToken).ConfigureAwait(false) is { } problem)
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
    private async Task<string?> StoredTextProblemAsync(string text, Guid userId, CancellationToken cancellationToken)
    {
        if (XsltSafety.FindThreat(text) is { } threat)
            return $"İşlenen tema güvenlik denetiminden geçmedi: {threat}";

        var compiled = await execution.CompileAsync(route: XsltRoute.Theme, text, userId, cancellationToken).ConfigureAwait(false);
        return compiled.Diagnostic switch
        {
            null => null,
            { } d when ThemeXsltRuns.IsOperational(d) => $"{EngineUnavailableMessage} ({d.Message})",
            { } d => $"İşlenen tema derlenemedi: {ThemeXsltRuns.Describe(d)}",
        };
    }

    // Tema örnek belgeyle derlenip render edilebilmeli (önizleme, compose ve analiz aynı motoru kullanır).
    private async Task<string?> RenderProblemAsync(string text, DocumentType type, Guid userId, CancellationToken cancellationToken)
    {
        var run = await execution.RenderAsync(route: XsltRoute.Theme, text, samples.Get(type), userId, cancellationToken).ConfigureAwait(false);
        if (run.Diagnostic is not { } error)
            return null;
        if (ThemeXsltRuns.IsOperational(error))
            return $"{EngineUnavailableMessage} ({error.Message})";

        var sample = type == DocumentType.Despatch ? "irsaliye" : "fatura";
        var hint = error.Kind == XsltFailureKind.Unsupported
            ? " Temalar en fazla XSLT 2.0 olabilir; XSLT 3.0 yapıları desteklenmez."
            : string.Empty;
        return $"Tema örnek {sample} ile işlenemedi, yüklenmedi: {ThemeXsltRuns.Describe(error)}{hint}";
    }
}
