using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;

namespace XsltCraft.Infrastructure.Ai;

/// <summary>Admin panelinden yönetilen AI runtime bayraklarının DB anahtarları (FeatureFlag.Key).</summary>
public static class AiFlagKeys
{
    public const string PreferredProvider = "ai.preferred_provider";
    public const string VisionEnabled = "ai.vision_enabled";
    public const string VisionGeminiFallback = "ai.vision_gemini_fallback";
}

/// <summary>
/// Görselli isteğin sağlayıcı sırası: runtime bayrakları (DB öncelikli, appsettings fallback) +
/// sağlayıcıların SupportsVision değeri → <see cref="ProviderRouting.ResolveVision"/>.
/// Bayrak okumaları AiFeatureFlagService'in 15 sn'lik cache'inden gelir.
/// </summary>
public class AiVisionRouter : IAiVisionAvailability
{
    private readonly IReadOnlyList<IAiAssistantProvider> _providers;
    private readonly IAiFeatureFlagService _flags;
    private readonly AiOptions _options;

    public AiVisionRouter(
        IEnumerable<IAiAssistantProvider> providers,
        IAiFeatureFlagService flags,
        IOptions<AiOptions> options)
    {
        _providers = providers.ToList();
        _flags = flags;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> ResolveProvidersAsync(CancellationToken ct = default)
    {
        var enabled = await _flags.GetBoolAsync(AiFlagKeys.VisionEnabled, ct) ?? _options.Vision.Enabled;
        if (!enabled) return [];

        var preferred = await _flags.GetStringAsync(AiFlagKeys.PreferredProvider, ct) ?? _options.PreferredProvider;
        var geminiFallback = await _flags.GetBoolAsync(AiFlagKeys.VisionGeminiFallback, ct) ?? _options.Vision.GeminiFallback;

        return ProviderRouting.ResolveVision(
            preferred,
            ollamaVision: Supports("ollama"),
            geminiVision: Supports("gemini"),
            geminiFallback);
    }

    private bool Supports(string name) => _providers.Any(p => p.Name == name && p.SupportsVision);
}
