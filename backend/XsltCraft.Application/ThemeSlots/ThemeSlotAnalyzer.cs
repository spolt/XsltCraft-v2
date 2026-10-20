using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots;

/// <param name="Xslt">İşaretli tema (başarısızlıkta işaretleri temizlenmiş özgün metin).</param>
public sealed record ThemeSlotAnalysis(string Xslt, ThemeSlotCatalog Catalog);

/// <summary>
/// Admin'in yüklediği temayı analiz eder: zorunlu alanları (çapaları) bulur, logo/imza konulabilecek
/// konumları (yuvaları) çıkarır, bunları XSLT'ye boş işaret olarak yazar ve örnek belgeyle render ederek
/// doğrular. Saf servis: string girer, string + katalog çıkar.
/// </summary>
public interface IThemeSlotAnalyzer
{
    /// <param name="imageRoles">Temadaki statik görsellerin rolleri, <see cref="DetectImages"/> sırasıyla. null ya da
    /// sayısı tutmuyorsa hepsi tasarım görseli sayılır.</param>
    ThemeSlotAnalysis Analyze(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null);

    /// <summary>Yuva yapılabilecek statik görseller ve önerilen rolleri (admin'e sorulur). Ayrıştırılamayan temada boş.</summary>
    IReadOnlyList<ThemeImageInfo> DetectImages(string xslt, DocumentType type);
}

public sealed class ThemeSlotAnalyzer(IXsltHtmlRenderer renderer, IThemeSampleXmlProvider samples) : IThemeSlotAnalyzer
{
    private readonly ThemeRenderVerifier _verifier = new(renderer, samples);

    public IReadOnlyList<ThemeImageInfo> DetectImages(string xslt, DocumentType type)
    {
        if (!XsltSourceMap.TryBuild(ThemeSlotMarkers.Clean(xslt), out var map, out _)
            || ThemeDocument.TryCreate(map, type) is not { } doc)
            return [];
        var anchors = new ThemeAnchorDetector().Detect(doc);
        return ThemeImageDetector.Detect(doc, anchors, new InsertionPointResolver(doc)).Select(i => i.ToInfo()).ToList();
    }

    public ThemeSlotAnalysis Analyze(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles = null)
    {
        // Admin'in "temadan kaldır" dediği görseller önce çıkarılır; analiz ve doğrulama kalan metin üzerinde.
        var stripped = ThemeImageStripper.Strip(xslt, type, imageRoles);
        var images = new List<(DetectedImage Image, ThemeImageRole Role, bool Removable)>();
        var result = AnalyzeCore(stripped.Xslt, type, occupiedHolderIds, stripped.Decisions, images);
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

    private ThemeSlotAnalysis AnalyzeCore(string xslt, DocumentType type, IReadOnlyCollection<string> occupiedHolderIds,
        IReadOnlyList<ThemeImageDecision>? imageRoles, List<(DetectedImage Image, ThemeImageRole Role, bool Removable)> images)
    {
        var baseXslt = ThemeSlotMarkers.Clean(xslt);
        var warnings = new List<ThemeSlotWarning>();

        if (!XsltSourceMap.TryBuild(baseXslt, out var map, out var mapError))
            return Fail(baseXslt, occupiedHolderIds, [], warnings, "parse-failed", $"Tema ayrıştırılamadı: {mapError}");

        var doc = ThemeDocument.TryCreate(map, type);
        if (doc is null)
            return Fail(baseXslt, occupiedHolderIds, [], warnings, "no-body-template", "Gövde (<body>) içeren bir kök şablon bulunamadı.");

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
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "write-failed", $"İşaretler yazılamadı: {ex.Message}");
            }

            if (XsltSafety.FindThreat(marked) is { } threat)
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "threat", threat);
            try
            {
                SecureXslt.Compile(marked);
            }
            catch (Exception ex)
            {
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "compile-failed", $"İşaretli tema derlenemedi: {ex.Message}");
            }

            var check = _verifier.Verify(baseXslt, marked, type, reduced.Select(r => r.Id));
            if (!check.Rendered)
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "render-failed", $"Tema örnek belgeyle render edilemedi: {check.Error}");
            if (!check.EquivalentAfterStrip)
                return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "render-mismatch", "İşaretler temanın çıktısını değiştirdi; konumlar eklenmedi.");

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

        return Fail(baseXslt, occupiedHolderIds, anchors.Report, warnings, "verify-failed", "Konumlar doğrulanamadı.");
    }

    private static List<(DetectedImage Image, ThemeImageRole Role, bool Removable)> AssignRoles(List<DetectedImage> detected,
        IReadOnlyList<ThemeImageDecision>? roles, List<ThemeSlotWarning> warnings)
    {
        if (roles is not null && roles.Count != detected.Count)
        {
            warnings.Add(new ThemeSlotWarning("image-roles-mismatch",
                $"Görsel kararları ({roles.Count}) temadaki görsel sayısıyla ({detected.Count}) eşleşmedi; görseller tasarımın parçası sayıldı."));
            roles = null;
        }
        return detected.Select((img, i) => roles?[i] is { } d
            ? (img, d.Role, d.Removable && d.Role != ThemeImageRole.Design)
            : (img, ThemeImageRole.Design, false)).ToList();
    }

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
        IReadOnlyList<ThemeAnchorResult> anchors, List<ThemeSlotWarning> warnings, string code, string message)
    {
        warnings.Add(new ThemeSlotWarning(code, message));
        return Done(baseXslt, ThemeSlotStatus.Failed, anchors, [], occupied, warnings);
    }
}
