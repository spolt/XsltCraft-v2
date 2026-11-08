using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.EasyEdit;

internal sealed record EasyEditVerdictInfo(EasyEditVerdict Verdict, string? ReasonCode, string? Message, int? ErrorLine);

/// <summary>Kolay düzenle karar kodları (tek kaynak).</summary>
internal static class EasyEditCodes
{
    public const string NoSlots = "no-slots";
    public const string ParseFailed = ThemeSlotAnalyzer.ParseFailedCode;
    public const string Threat = ThemeSlotAnalyzer.ThreatCode;
    public const string Unsupported = "unsupported";
    public const string NotUtf8 = "not-utf8";
    public const string TooHeavy = "too-heavy";
    public const string EngineMismatch = ThemeSlotAnalyzer.RenderEngineMismatchCode;
    public const string ImageRolesMismatch = ThemeSlotAnalyzer.ImageRolesMismatchCode;
}

/// <summary>Yuva analizinin durumunu ve yapısal başarısızlık bilgisini kolay düzenle kararına çevirir.</summary>
internal static class EasyEditVerdictMapper
{
    public const string RejectedMessage = "Dosya güvenlik denetiminden geçmedi; kolay düzenleme yapılamaz.";
    public const string UnavailableMessage = "Analiz şu anda yapılamıyor, birazdan tekrar deneyin.";
    public const string TooHeavyMessage = "Dosya örnek belgeyle işlenirken süre, bellek ya da çıktı sınırını aştı; kolay düzenleme yapılamaz.";

    public static EasyEditVerdictInfo Map(ThemeSlotAnalysis analysis)
    {
        var catalog = analysis.Catalog;
        var last = catalog.Warnings.Count > 0 ? catalog.Warnings[^1] : null;
        return catalog.Status switch
        {
            ThemeSlotStatus.Ok => new(EasyEditVerdict.Suitable, null, null, null),
            ThemeSlotStatus.Partial => new(EasyEditVerdict.Partial, null, null, null),
            ThemeSlotStatus.None => new(EasyEditVerdict.Unsuitable, EasyEditCodes.NoSlots,
                "Dosyada logo ya da imza eklenebilecek bir konum bulunamadı.", null),
            ThemeSlotStatus.Failed => Failed(analysis.Failure, last?.Message),
            _ => new(EasyEditVerdict.Unsuitable, last?.Code, last?.Message, null),
        };
    }

    private static EasyEditVerdictInfo Failed(ThemeSlotFailure? failure, string? message) => failure?.Code switch
    {
        ThemeSlotAnalyzer.EngineUnavailableCode when failure.Kind == XsltFailureKind.UserBusy =>
            new(EasyEditVerdict.Busy, failure.Code, UnavailableMessage, null),
        ThemeSlotAnalyzer.EngineUnavailableCode => new(EasyEditVerdict.Unavailable, failure.Code, UnavailableMessage, null),
        ThemeSlotAnalyzer.ThreatCode or ThemeSlotAnalyzer.RenderRejectedCode =>
            new(EasyEditVerdict.Rejected, EasyEditCodes.Threat, RejectedMessage, null),
        ThemeSlotAnalyzer.RenderTooHeavyCode or ThemeSlotAnalyzer.RenderLimitCode => new(EasyEditVerdict.Unsuitable, EasyEditCodes.TooHeavy, TooHeavyMessage, null),
        ThemeSlotAnalyzer.RenderEngineMismatchCode => new(EasyEditVerdict.Unsuitable, failure.Code, message, null),
        ThemeSlotAnalyzer.ParseFailedCode or ThemeSlotAnalyzer.RenderFailedCode =>
            new(EasyEditVerdict.Invalid, failure.Code, message, failure.Line > 0 ? failure.Line : null),
        _ => new(EasyEditVerdict.Unsuitable, failure?.Code, message, null),
    };
}
