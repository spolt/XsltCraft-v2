using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.EasyEdit;

/// <summary>
/// Kullanıcının yüklediği XSLT'yi kolay düzenleme için analiz eder: güvenlik taraması, önceki görsel bloğunun
/// geri okunması ve temizlenmesi, görsel rollerinin seçimi (istemci &gt; önceki işaret &gt; öneri) ve yuva analizi
/// (<see cref="XsltRoute.Raw"/>). Görsel verisi ve dosya içeriği loglanmaz.
/// </summary>
public sealed class UploadedXsltAnalyzer(
    IXsltSafetyScanner safety,
    IThemeOverridesProcessor overrides,
    IThemeSlotAnalyzer analyzer) : IUploadedXsltAnalyzer
{
    /// <summary>Yanıttaki en fazla görsel; fazlası tasarım görseli sayılır.</summary>
    public const int MaxImages = 20;

    public async Task<EasyEditAnalysis> AnalyzeAsync(string xslt, IReadOnlyList<ThemeImageRole>? clientRoles, Guid userId,
        CancellationToken cancellationToken)
    {
        var text = ThemeTextPrelude.StripBom(xslt);
        var type = ThemeDocumentTypeSniffer.Sniff(text);

        var report = safety.Scan(text, XsltSafetyMode.Execute);
        if (report.Findings.Any(f => f.Severity == XsltSafetySeverity.Security && f.Code != XsltSafetyScanner.MalformedXmlCode))
            return Early(EasyEditVerdict.Rejected, EasyEditCodes.Threat, EasyEditVerdictMapper.RejectedMessage, type);
        // İyi biçimli olmayan XML tarayıcıda fail-closed tehdittir; kullanıcıya satırıyla "geçersiz" olarak döner (yürütme yok).
        if (report.Findings.FirstOrDefault(f => f.Code == XsltSafetyScanner.MalformedXmlCode) is { } malformed)
            return Early(EasyEditVerdict.Invalid, EasyEditCodes.ParseFailed, malformed.Message, type, malformed.Line > 0 ? malformed.Line : null);
        if (report.Findings.FirstOrDefault(f => f.Severity == XsltSafetySeverity.Unsupported) is { } unsupported)
            return Early(EasyEditVerdict.Invalid, EasyEditCodes.Unsupported, unsupported.Message, type, unsupported.Line > 0 ? unsupported.Line : null);

        if (ThemeTextPrelude.NonUtf8DeclaredEncoding(text) is { } encoding)
            return Early(EasyEditVerdict.Unsuitable, EasyEditCodes.NotUtf8, $"Dosya {encoding} kodlamalı; kolay düzenleme yalnız UTF-8 dosyalarda yapılır.", type);

        var inspection = overrides.Inspect(text);
        if (inspection.Present || inspection.DanglingMarkers > 0)
            text = overrides.Apply(text, OverridesDecision.Strip).Xslt;
        var previous = inspection.Present
            ? new EasyEditPrevious(inspection.Recognized, inspection.Reason, inspection.Images, inspection.Hide ?? [], inspection.Bank ?? [],
                inspection.FreeImages ?? [])
            : null;

        // Önceki roller işaretler temizlenmeden okunur (blok kaldırma işaretlere dokunmaz; görsel sırası aynı kalır).
        var prior = PriorImageRoleReader.Read(text, type);
        var detected = prior.Detected;
        var clientMismatch = clientRoles is not null && clientRoles.Count != Math.Min(detected.Count, MaxImages);
        var roles = ChooseRoles(detected, prior.Roles, clientMismatch ? null : clientRoles);
        var decisions = roles.Select(r => new ThemeImageDecision(r.Role, Removable: r.Role != ThemeImageRole.Design, Strip: false)).ToList();

        var analysis = await analyzer.AnalyzeAsync(text, type, [], decisions.Count > 0 ? decisions : null, userId, XsltRoute.Raw,
            cancellationToken).ConfigureAwait(false);
        if (clientMismatch)
            analysis = analysis with { Catalog = WithClientMismatch(analysis.Catalog, clientRoles!.Count, Math.Min(detected.Count, MaxImages)) };
        var verdict = EasyEditVerdictMapper.Map(analysis);
        var usable = verdict.Verdict is EasyEditVerdict.Suitable or EasyEditVerdict.Partial;

        // Notlar yalnız kullanılabilir metinde (başarısızlıkta metin döndürülmez).
        var notesSupported = usable && FixedNoteWriter.Apply(analysis.Xslt, null, type).Supported;
        var notes = usable ? FixedNoteReader.Read(analysis.Xslt, type) : [];
        var images = Images(detected, prior.Dynamic, roles, analysis.Catalog);
        // Banka satırı index'leri compose'a gönderilecek bu metne göredir.
        var documentBank = usable ? DocumentBankDetector.Detect(analysis.Xslt!, type).ToInfo() : null;

        return new EasyEditAnalysis(verdict.Verdict, verdict.ReasonCode, verdict.Message, usable ? analysis.Xslt : null, type,
            analysis.Catalog, images, previous, notes, notesSupported, verdict.Verdict == EasyEditVerdict.Invalid ? verdict.ErrorLine : null, documentBank);
    }

    // Başarısızlık uyarısı (varsa) son uyarı kalır.
    private static ThemeSlotCatalog WithClientMismatch(ThemeSlotCatalog catalog, int sent, int shown)
    {
        var warning = new ThemeSlotWarning(EasyEditCodes.ImageRolesMismatch,
            $"Gönderilen görsel rolleri ({sent}) dosyadaki görsel sayısıyla ({shown}) eşleşmedi; seçim yok sayıldı.");
        var warnings = catalog.Warnings.ToList();
        warnings.Insert(catalog.Status == ThemeSlotStatus.Failed && warnings.Count > 0 ? warnings.Count - 1 : warnings.Count, warning);
        return catalog with { Warnings = warnings };
    }

    // İstemci kararı (yanıttaki görsel sayısıyla tutuyorsa; çağıran denetler) > önceki işaret > öneri. İlk MaxImages dışındakiler tasarım.
    private static List<(ThemeImageRole Role, EasyEditRoleSource Source)> ChooseRoles(IReadOnlyList<ThemeImageInfo> detected,
        IReadOnlyList<ThemeImageRole?> prior, IReadOnlyList<ThemeImageRole>? client)
    {
        var priorAligned = prior.Count == detected.Count ? prior : null;

        return detected.Select((info, i) =>
        {
            if (i >= MaxImages)
                return (ThemeImageRole.Design, EasyEditRoleSource.Suggested);
            if (client is not null)
                return (client[i], EasyEditRoleSource.Client);
            if (priorAligned?[i] is { } marker)
                return (marker, EasyEditRoleSource.Marker);
            return (info.Suggested, EasyEditRoleSource.Suggested);
        }).ToList();
    }

    // Taşınabilirlik görsel doğrulamasından bağımsızdır (StaticImagePlan ile aynı kural; StaticImageSources).
    private static List<EasyEditImage> Images(IReadOnlyList<ThemeImageInfo> detected, IReadOnlyList<bool> dynamic,
        List<(ThemeImageRole Role, EasyEditRoleSource Source)> roles, ThemeSlotCatalog catalog)
    {
        var slotByIndex = (catalog.Images ?? []).GroupBy(r => r.Index).ToDictionary(g => g.Key, g => g.First().SlotId);
        return detected.Take(MaxImages).Select((info, i) =>
            {
                var blocker = StaticImageSources.Blocker(info.PreviewDataUrl, i < dynamic.Count && dynamic[i]);
                return new EasyEditImage(info.Index, roles[i].Role, roles[i].Source, info.Reason, info.Line, info.PreviewDataUrl,
                    Movable: blocker is null, StaticBlocker: blocker, SlotId: slotByIndex.GetValueOrDefault(info.Index));
            })
            .ToList();
    }

    private static EasyEditAnalysis Early(EasyEditVerdict verdict, string code, string message, DocumentType type, int? line = null) =>
        new(verdict, code, message, null, type, null, [], null, [], false, verdict == EasyEditVerdict.Invalid ? line : null);
}
