using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Infrastructure.Tests.Ai;

public class AiVisionRouterTests
{
    private sealed class FakeProvider(string name, bool vision) : IAiAssistantProvider
    {
        public string Name => name;
        public bool SupportsVision => vision;
        public IAsyncEnumerable<AiChunk> StreamAsync(AiRequest req, string prompt, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FakeFlags(Dictionary<string, string?> values) : IAiFeatureFlagService
    {
        public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task SetEnabledAsync(bool enabled, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetStringAsync(string key, CancellationToken ct = default)
            => Task.FromResult(values.GetValueOrDefault(key));
        public Task SetStringAsync(string key, string? value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool?> GetBoolAsync(string key, CancellationToken ct = default)
            => Task.FromResult(bool.TryParse(values.GetValueOrDefault(key), out var b) ? b : (bool?)null);
        public Task SetBoolAsync(string key, bool value, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AiVisionRouter Router(
        Dictionary<string, string?> flags,
        bool ollamaVision = false,
        bool geminiVision = true,
        bool configEnabled = false,
        bool configFallback = true)
        => new(
            [new FakeProvider("ollama", ollamaVision), new FakeProvider("gemini", geminiVision)],
            new FakeFlags(flags),
            Options.Create(new AiOptions
            {
                PreferredProvider = "auto",
                Vision = new VisionOptions { Enabled = configEnabled, GeminiFallback = configFallback },
            }));

    [Fact]
    public async Task VisionDisabled_ReturnsEmpty()
        => Assert.Empty(await Router([]).ResolveProvidersAsync());

    [Fact]
    public async Task DbFlag_OverridesConfig()
    {
        var router = Router(new() { [AiFlagKeys.VisionEnabled] = "false" }, configEnabled: true);
        Assert.Empty(await router.ResolveProvidersAsync());
    }

    [Fact]
    public async Task Enabled_AutoPreference_GeminiFirst()
    {
        var router = Router(new() { [AiFlagKeys.VisionEnabled] = "true" }, ollamaVision: true);
        Assert.Equal(["gemini", "ollama"], await router.ResolveProvidersAsync());
    }

    [Fact]
    public async Task OllamaPreferred_NoLocalModel_FallbackFromConfig_UsesGemini()
    {
        var router = Router(new()
        {
            [AiFlagKeys.VisionEnabled] = "true",
            [AiFlagKeys.PreferredProvider] = "ollama",
        });
        Assert.Equal(["gemini"], await router.ResolveProvidersAsync());
    }

    [Fact]
    public async Task OllamaPreferred_FallbackDisabledInDb_StrictLocal()
    {
        var router = Router(new()
        {
            [AiFlagKeys.VisionEnabled] = "true",
            [AiFlagKeys.PreferredProvider] = "ollama",
            [AiFlagKeys.VisionGeminiFallback] = "false",
        });
        Assert.Empty(await router.ResolveProvidersAsync());
    }

    [Fact]
    public async Task OllamaPreferred_WithLocalModel_LocalFirstGeminiFallback()
    {
        var router = Router(new()
        {
            [AiFlagKeys.VisionEnabled] = "true",
            [AiFlagKeys.PreferredProvider] = "ollama",
        }, ollamaVision: true);
        Assert.Equal(["ollama", "gemini"], await router.ResolveProvidersAsync());
    }
}
