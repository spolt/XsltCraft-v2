namespace XsltCraft.Application.Ai.Vision;

/// <summary>
/// Görselli istek hangi sağlayıcılara, hangi sırayla gidebilir? Runtime bayraklarını
/// (ai.vision_enabled, ai.preferred_provider, ai.vision_gemini_fallback) ve sağlayıcıların
/// <see cref="IAiAssistantProvider.SupportsVision"/> değerini birleştirir. Boş liste = vision kapalı/kullanılamaz.
/// Orkestratör ve <see cref="IAiVisionGate"/> aynı kararı kullanır.
/// </summary>
public interface IAiVisionAvailability
{
    Task<IReadOnlyList<string>> ResolveProvidersAsync(CancellationToken ct = default);
}

/// <summary>Kullanıcı başına görselli istek hız sınırı (ai-assistant politikası gövdeye bakamaz).</summary>
public interface IAiVisionThrottle
{
    bool TryAcquire(Guid userId);
}
