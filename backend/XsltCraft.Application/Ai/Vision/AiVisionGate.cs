using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Ai.Vision;

public enum VisionDenyReason
{
    None,
    Unavailable,
    CountExceeded,
    RateLimited,
    Invalid,
}

public sealed record AiVisionGateResult(
    IReadOnlyList<AiImageInput> Images,
    VisionDenyReason Reason,
    string? ErrorCode = null,
    string? Message = null,
    bool Upgrade = false)
{
    public bool Allowed => Reason == VisionDenyReason.None;

    internal static AiVisionGateResult Deny(VisionDenyReason reason, string code, string message, bool upgrade = false)
        => new([], reason, code, message, upgrade);
}

public interface IAiVisionGate
{
    /// <summary>
    /// Görselli isteğin kapısı — kota kontrolünden ÖNCE çağrılır ki reddedilen istek kullanıcının
    /// günlük hakkını tüketmesin. Sıra ucuzdan pahalıya: uygunluk → plan limiti → throttle → decode/doğrulama.
    /// </summary>
    Task<AiVisionGateResult> EvaluateAsync(Guid userId, IReadOnlyList<AiImagePayload> images, CancellationToken ct = default);
}

public sealed class AiVisionGate : IAiVisionGate
{
    public const string CodeUnavailable = "vision_unavailable";
    public const string CodeRateLimited = "vision_rate_limited";

    private readonly IAiVisionAvailability _availability;
    private readonly IEntitlementService _entitlements;
    private readonly IAiVisionThrottle _throttle;
    private readonly IAiImageValidator _validator;
    private readonly VisionOptions _options;

    public AiVisionGate(
        IAiVisionAvailability availability,
        IEntitlementService entitlements,
        IAiVisionThrottle throttle,
        IAiImageValidator validator,
        VisionOptions options)
    {
        _availability = availability;
        _entitlements = entitlements;
        _throttle = throttle;
        _validator = validator;
        _options = options;
    }

    public async Task<AiVisionGateResult> EvaluateAsync(Guid userId, IReadOnlyList<AiImagePayload> images, CancellationToken ct = default)
    {
        if (images.Count == 0) return new([], VisionDenyReason.None);

        var providers = await _availability.ResolveProvidersAsync(ct);
        if (providers.Count == 0)
            return AiVisionGateResult.Deny(VisionDenyReason.Unavailable, CodeUnavailable,
                "Ekran görüntüsü analizi şu an kullanılamıyor: uygun bir AI sağlayıcısı yapılandırılmamış.");

        var ent = await _entitlements.GetAsync(userId, ct);
        var limit = EffectiveLimit(ent.MaxAiImagesPerMessage, _options.MaxImagesPerMessage);
        if (images.Count > limit)
        {
            var upgrade = !ent.IsPrivileged && ent.EffectivePlan == MembershipPlan.Free
                          && images.Count <= _options.MaxImagesPerMessage;
            var message = upgrade
                ? $"Ücretsiz planda mesaj başına {limit} ekran görüntüsü ekleyebilirsiniz. Daha fazlası için XsltCraft Pro'ya geçin."
                : $"Mesaj başına en fazla {limit} ekran görüntüsü eklenebilir.";
            return AiVisionGateResult.Deny(VisionDenyReason.CountExceeded, AiImageValidator.CodeCount, message, upgrade);
        }

        // Throttle decode'dan önce: kullanıcı başına CPU/bellek sınırlanır.
        if (!_throttle.TryAcquire(userId))
            return AiVisionGateResult.Deny(VisionDenyReason.RateLimited, CodeRateLimited,
                "Çok sık ekran görüntüsü gönderildi. Lütfen bir dakika sonra tekrar deneyin.");

        var validation = _validator.Validate(images, limit);
        if (!validation.IsValid)
            return AiVisionGateResult.Deny(VisionDenyReason.Invalid, validation.ErrorCode!, validation.Message!);

        return new(validation.Images, VisionDenyReason.None);
    }

    /// <summary>Plan limiti (0 = sınırsız) teknik tavanı asla aşamaz.</summary>
    internal static int EffectiveLimit(int planLimit, int technicalCap)
        => planLimit <= 0 ? technicalCap : Math.Min(planLimit, technicalCap);
}
