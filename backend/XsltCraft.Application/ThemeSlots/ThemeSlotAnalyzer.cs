using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots;

/// <param name="Xslt">İşaretli tema (başarısızlıkta işaretleri temizlenmiş özgün metin).</param>
/// <param name="Failure">Durum <see cref="ThemeSlotStatus.Failed"/> ise başarısızlığın yapısal bilgisi (katalogda saklanmaz).</param>
public sealed record ThemeSlotAnalysis(string Xslt, ThemeSlotCatalog Catalog, ThemeSlotFailure? Failure = null);

/// <param name="Code">Başarısızlık uyarısının kodu (kataloğun son uyarısı).</param>
/// <param name="Kind">Motor hatasıyla başarısız olduysa hatanın türü.</param>
/// <param name="Line">Hatanın satırı (bilinmiyorsa 0).</param>
public sealed record ThemeSlotFailure(string Code, XsltFailureKind? Kind = null, int Line = 0);

/// <summary>
/// Admin'in yüklediği temayı analiz eder: zorunlu alanları (çapaları) bulur, logo/imza konulabilecek
/// konumları (yuvaları) çıkarır, bunları XSLT'ye boş işaret olarak yazar ve örnek belgeyle render ederek
/// doğrular. String girer, string + katalog çıkar; render/derleme tema hattından (<see cref="ThemeXsltRuns"/>:
/// 1.0 → .NET süreç içi, 2.0 → Saxon izole worker).
/// </summary>
public interface IThemeSlotAnalyzer
{
    /// <param name="imageRoles">Temadaki statik görsellerin rolleri, <see cref="DetectImages"/> sırasıyla. null ya da
    /// sayısı tutmuyorsa hepsi tasarım görseli sayılır.</param>
    /// <param name="userId">Yürütme kapısı bölümü (yükleyen admin ya da kullanıcı).</param>
    /// <param name="route">Admin teması için <see cref="XsltRoute.Theme"/>, kullanıcının yüklediği XSLT için <see cref="XsltRoute.Raw"/>.</param>
    Task<ThemeSlotAnalysis> AnalyzeAsync(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles, Guid userId, XsltRoute route, CancellationToken cancellationToken = default);

    /// <summary>Yuva yapılabilecek statik görseller ve önerilen rolleri (admin'e sorulur). Ayrıştırılamayan temada boş.</summary>
    IReadOnlyList<ThemeImageInfo> DetectImages(string xslt, DocumentType type);
}

public sealed class ThemeSlotAnalyzer(IXsltExecutionService execution, IThemeSampleXmlProvider samples) : IThemeSlotAnalyzer
{
    /// <summary>Analiz, motor geçici olarak kullanılamadığı için yapılamadı (tema hatası değil; yükleme reddedilir).</summary>
    public const string EngineUnavailableCode = "engine-unavailable";

    // Başarısızlık kodları (Failed durumunda kataloğun son uyarısı).
    internal const string ParseFailedCode = "parse-failed";
    internal const string NoBodyTemplateCode = "no-body-template";
    internal const string WriteFailedCode = "write-failed";
    internal const string ThreatCode = "threat";
    internal const string CompileFailedCode = "compile-failed";
    /// <summary>Render motor hatasıyla yapılamadı (aşağıdaki özel durumlar dışında).</summary>
    internal const string RenderFailedCode = "render-failed";
    /// <summary>Render süre, bellek ya da çıktı sınırına takıldı.</summary>
    internal const string RenderTooHeavyCode = "render-too-heavy";
    /// <summary>Render çıktısı karşılaştırma sınırını aştı.</summary>
    internal const string RenderLimitCode = "render-limit";
    /// <summary>Render motorun güvenlik denetimince reddedildi.</summary>
    internal const string RenderRejectedCode = "render-rejected";
    /// <summary>İşaretli ve işaretsiz sürüm farklı motorlarla çalıştı.</summary>
    internal const string RenderEngineMismatchCode = "render-engine-mismatch";
    internal const string RenderMismatchCode = "render-mismatch";
    internal const string VerifyFailedCode = "verify-failed";

    internal const string ImageRolesMismatchCode = "image-roles-mismatch";

    private static readonly HashSet<string> FailureCodes = new(StringComparer.Ordinal)
    {
        EngineUnavailableCode, ParseFailedCode, NoBodyTemplateCode, WriteFailedCode, ThreatCode, CompileFailedCode,
        RenderFailedCode, RenderTooHeavyCode, RenderLimitCode, RenderRejectedCode, RenderEngineMismatchCode, RenderMismatchCode, VerifyFailedCode,
    };

    private readonly ThemeRenderVerifier _verifier = new(execution, samples);

    public IReadOnlyList<ThemeImageInfo> DetectImages(string xslt, DocumentType type)
    {
        if (!XsltSourceMap.TryBuild(ThemeSlotMarkers.Clean(xslt), out var map, out _)
            || ThemeDocument.TryCreate(map, type) is not { } doc)
            return [];
        var anchors = new ThemeAnchorDetector().Detect(doc);
        return ThemeImageDetector.Detect(doc, anchors, new InsertionPointResolver(doc)).Select(i => i.ToInfo()).ToList();
    }

    public async Task<ThemeSlotAnalysis> AnalyzeAsync(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles, Guid userId, XsltRoute route, CancellationToken cancellationToken = default)
    {
        // Admin'in "temadan kaldır" dediği görseller önce çıkarılır; analiz ve doğrulama kalan metin üzerinde.
        var stripped = ThemeImageStripper.Strip(xslt, type, imageRoles);
        var images = new List<(DetectedImage Image, ThemeImageRole Role, bool Removable)>();
        var result = await AnalyzeCoreAsync(stripped.Xslt, type, occupiedHolderIds, stripped.Decisions, images, userId, route, cancellationToken)
            .ConfigureAwait(false);
        if (images.Count == 0 && stripped.Removed.Count == 0)
            return result;

        // Görsel kararları rapor için kataloğa (özgün sırayla); yuvası doğrulamadan geçemediyse SlotId boş.
        var slotIds = result.Catalog.Slots.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var records = images.Select(i => new ThemeImageRecord(
                i.Image.Index < stripped.OriginalIndex.Count ? stripped.OriginalIndex[i.Image.Index] : i.Image.Index, i.Role, i.Image.Line,
                SlotIdFor(i.Role) is { } id && slotIds.Contains(id) ? id : null, i.Removable,
                Stripped: stripped.Decisions?.ElementAtOrDefault(i.Image.Index)?.Strip == true))
            .Concat(stripped.Removed.Select(r => new ThemeImageRecord(r.Index, ThemeImageRole.Design, r.Line, null, Stripped: true)))
            .OrderBy(r => r.Index)
            .ToList();
        return result with { Catalog = result.Catalog with { Images = records } };
    }

    private async Task<ThemeSlotAnalysis> AnalyzeCoreAsync(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles, List<(DetectedImage Image, ThemeImageRole Role, bool Removable)> images,
        Guid userId, XsltRoute route, CancellationToken cancellationToken)
    {
        var baseXslt = ThemeSlotMarkers.Clean(xslt);
        var warnings = new List<ThemeSlotWarning>();

        if (!XsltSourceMap.TryBuild(baseXslt, out var map, out var mapError, out var errorLine))
            return Fail(baseXslt, occupiedHolderIds, [], warnings, new(ParseFailedCode, Line: errorLine), $"Tema ayrıştırılamadı: {mapError}");

        var doc = ThemeDocument.TryCreate(map, type);
        if (doc is null)
            return Fail(baseXslt, occupiedHolderIds, [], warnings, new(NoBodyTemplateCode), "Gövde (<body>) içeren bir kök şablon bulunamadı.");

        var anchors = new ThemeAnchorDetector().Detect(doc);
        foreach (var filled in ThemeAnchorDetector.FilledHolders(doc))
            warnings.Add(new ThemeSlotWarning("holder-has-content",
                $"#{(string?)filled.Attribute("id")} içeriği dolu olduğu için konum olarak sunulmadı."));

        var resolver = new InsertionPointResolver(doc);
        images.AddRange(AssignRoles(ThemeImageDetector.Detect(doc, anchors, resolver, warnings), imageRoles, warnings));

        var candidates = new ThemeSlotRules(doc, anchors, resolver, images).Propose(warnings);
        var reduced = SlotCandidateReducer.Reduce(doc, candidates, warnings);
        if (reduced.Count == 0)
            return Done(baseXslt, ThemeSlotStatus.None, anchors.Report, [], occupiedHolderIds, warnings);

        // Doğrula; beklenen sayıda görünmeyen yuvaları at ve bir kez yeniden dene.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            string marked;
            try
            {
                marked = ThemeSlotMarkers.Write(doc, reduced);
            }
            catch (InvalidOperationException ex)
            {
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, new(WriteFailedCode), $"İşaretler yazılamadı: {ex.Message}");
            }

            if (XsltSafety.FindThreat(marked) is { } threat)
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, new(ThreatCode), threat);
            // Raw rotada ayrı derleme yok: Validate orada 1 MiB / 5 sn sınırında; işaretli render zaten derler.
            if (route == XsltRoute.Theme)
            {
                var compiled = await execution.CompileAsync(route, marked, userId, cancellationToken).ConfigureAwait(false);
                if (compiled.Diagnostic is { } compileError)
                {
                    var operational = ThemeXsltRuns.IsOperational(compileError);
                    return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings,
                        new(operational ? EngineUnavailableCode : CompileFailedCode, compileError.Kind, compileError.Line),
                        operational
                            ? $"XSLT motoru kullanılamadı: {compileError.Message}"
                            : $"İşaretli tema derlenemedi: {ThemeXsltRuns.Describe(compileError)}");
                }
            }

            var check = await _verifier.VerifyAsync(baseXslt, marked, type, reduced.Select(r => r.Id), route, userId, cancellationToken)
                .ConfigureAwait(false);
            if (!check.Rendered)
            {
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, new(RenderFailureCode(check), check.Kind, check.Line),
                    check.Operational ? $"XSLT motoru kullanılamadı: {check.Error}" : $"Tema örnek belgeyle render edilemedi: {check.Error}");
            }
            if (!check.EquivalentAfterStrip)
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, new(RenderMismatchCode), "İşaretler temanın çıktısını değiştirdi; konumlar eklenmedi.");

            var bad = reduced.Where(r => !Expected(r, check.Occurrences.GetValueOrDefault(r.Id))).ToList();
            if (bad.Count == 0)
            {
                var slots = reduced.Select(r => ToSlot(doc, r)).ToList();
                var status = Status(anchors, slots.Count, warnings);
                return Done(marked, status, anchors.Report, slots, occupiedHolderIds, warnings);
            }

            foreach (var r in bad)
            {
                var count = check.Occurrences.GetValueOrDefault(r.Id);
                warnings.Add(new ThemeSlotWarning(count == 0 ? "slot-dropped-missing" : "slot-dropped-not-unique",
                    count == 0
                        ? $"{r.Id} örnek belgenin çıktısında görünmedi; konum olarak sunulmadı."
                        : $"{r.Id} çıktıda {count} kez tekrarlandı; konum olarak sunulmadı.", r.Id));
            }

            reduced = reduced.Except(bad).ToList();
            if (reduced.Count == 0)
                return Done(baseXslt, ThemeSlotStatus.None, anchors.Report, [], occupiedHolderIds, warnings);
        }

        return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, new(VerifyFailedCode), "Konumlar doğrulanamadı.");
    }

    private static List<(DetectedImage Image, ThemeImageRole Role, bool Removable)> AssignRoles(List<DetectedImage> detected,
        IReadOnlyList<ThemeImageDecision>? roles, List<ThemeSlotWarning> warnings)
    {
        if (roles is not null && roles.Count != detected.Count)
        {
            warnings.Add(new ThemeSlotWarning(ImageRolesMismatchCode,
                $"Görsel kararları ({roles.Count}) temadaki görsel sayısıyla ({detected.Count}) eşleşmedi; görseller tasarımın parçası sayıldı."));
            roles = null;
        }
        return detected.Select((img, i) => roles?[i] is { } d
            ? (img, d.Role, d.Removable && d.Role != ThemeImageRole.Design)
            : (img, ThemeImageRole.Design, false)).ToList();
    }

    /// <summary>Kod bir analiz başarısızlığını bildirir (bilgi uyarısı değil).</summary>
    internal static bool IsFailureCode(string code) => FailureCodes.Contains(code);

    private static string RenderFailureCode(RenderVerification check) => check switch
    {
        { Operational: true } => EngineUnavailableCode,
        { Failure: RenderFailure.OutputLimit } => RenderLimitCode,
        { Failure: RenderFailure.EngineMismatch } => RenderEngineMismatchCode,
        { Kind: XsltFailureKind.Timeout or XsltFailureKind.MemoryLimit or XsltFailureKind.OutputLimit } => RenderTooHeavyCode,
        { Kind: XsltFailureKind.Security } => RenderRejectedCode,
        _ => RenderFailedCode,
    };

    private static string? SlotIdFor(ThemeImageRole role) => role switch
    {
        ThemeImageRole.Logo => "existing-logo",
        ThemeImageRole.Signature => "existing-signature",
        _ => null,
    };

    // Koşulsuz yuva örnek çıktıda tam bir kez, koşullu yuva en fazla bir kez görünmeli; temadaki görsel yuvası
    // görsel sayısı kadar (koşullu olanlar çıkmayabilir).
    private static bool Expected(ReducedSlot r, int count)
    {
        if (r.Candidate.Placeholders is { } placeholders)
            return count <= placeholders.Count && count >= placeholders.Count(e => !InsertionPointResolver.IsConditional(e));
        return r.Candidate.Conditional ? count <= 1 : count == 1;
    }

    private static ThemeSlot ToSlot(ThemeDocument d, ReducedSlot r)
    {
        var c = r.Candidate;
        var offset = c.Placeholders is { Count: > 0 } placeholders ? d.Map.Span(placeholders[0]).Start
            : c.AdoptedHolder is not null ? d.Map.Span(c.AdoptedHolder).Start
            : c.Point!.Offset;
        return new ThemeSlot(r.Id, c.Rule, c.Order, c.Layout, r.DefaultFor, c.Conditional,
            SlotCandidateReducer.LegacyIdOf(c), Inserted: c.AdoptedHolder is null && c.Placeholders is null, Line: d.Map.LineOf(offset),
            Removable: c.RemovablePlaceholders is { Count: > 0 });
    }

    private static ThemeSlotStatus Status(ThemeAnchors a, int slotCount, List<ThemeSlotWarning> warnings)
    {
        if (slotCount == 0)
            return ThemeSlotStatus.None;
        var coreFound = a.Seller is not null && a.Qr is not null && a.Customer is not null && a.DocInfo is not null;
        var dropped = warnings.Any(w => w.Code.StartsWith("slot-dropped", StringComparison.Ordinal));
        return coreFound && !dropped ? ThemeSlotStatus.Ok : ThemeSlotStatus.Partial;
    }

    private static ThemeSlotAnalysis Done(string xslt, ThemeSlotStatus status, IReadOnlyList<ThemeAnchorResult> anchors,
        IReadOnlyList<ThemeSlot> slots, IReadOnlyCollection<string> occupied, List<ThemeSlotWarning> warnings) =>
        new(xslt, new ThemeSlotCatalog(ThemeSlotCatalog.CurrentVersion, ThemeSlotCatalog.CurrentAnalyzerVersion, status,
            DateTime.UtcNow, anchors, slots, occupied.ToList(), Overrides: null, warnings));

    private static ThemeSlotAnalysis Fail(string baseXslt, IReadOnlyCollection<string> occupied,
        IReadOnlyList<ThemeAnchorResult> anchors, List<ThemeSlotWarning> warnings, ThemeSlotFailure failure, string message)
    {
        warnings.Add(new ThemeSlotWarning(failure.Code, message));
        return Done(baseXslt, ThemeSlotStatus.Failed, anchors, [], occupied, warnings) with { Failure = failure };
    }
}
