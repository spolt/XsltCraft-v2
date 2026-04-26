using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;

namespace XsltCraft.Infrastructure.Ai;

/// <summary>
/// Deterministik fallback: varsayılan Ollama → Gemini, tercih "gemini" ise Gemini → Ollama.
/// Mid-stream fallback yok — ilk chunk geldikten sonra hata olursa kullanıcıya hata chunk'ı gönderilir.
/// </summary>
public class AiProviderOrchestrator
{
    private readonly OllamaAssistantProvider _ollama;
    private readonly IReadOnlyList<IAiAssistantProvider> _others; // Ollama dışı sağlayıcılar
    private readonly IAiFeatureFlagService _flagService;
    private readonly AiOptions _options;
    private readonly ILogger<AiProviderOrchestrator> _logger;

    public AiProviderOrchestrator(
        OllamaAssistantProvider ollama,
        IEnumerable<IAiAssistantProvider> allProviders,
        IAiFeatureFlagService flagService,
        IOptions<AiOptions> options,
        ILogger<AiProviderOrchestrator> logger)
    {
        _ollama = ollama;
        _others = allProviders.Where(p => p.Name != ollama.Name).ToList();
        _flagService = flagService;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest req,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var prompt = PromptTemplates.Build(req);

        var preferred = await _flagService.GetStringAsync("ai.preferred_provider", ct)
                        ?? _options.PreferredProvider;

        // "gemini" öncelikli: Gemini varsa önce dene, Ollama yedek.
        List<IAiAssistantProvider> providers = preferred == "gemini" && _others.Count > 0
            ? [.._others, _ollama]
            : [(IAiAssistantProvider)_ollama, .._others];

        Exception? lastError = null;
        for (int i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            var enumerator = provider.StreamAsync(req, prompt, ct).GetAsyncEnumerator(ct);
            bool firstChunkReceived = false;
            try
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                    }
                    catch (Exception ex) when (!firstChunkReceived && !ct.IsCancellationRequested)
                    {
                        lastError = ex;
                        var nextName = i + 1 < providers.Count ? providers[i + 1].Name : "(none)";
                        var reason = (ex as AiProviderTimeoutException)?.Code
                                     ?? (ex as AiProviderUnavailableException)?.Code
                                     ?? ex.GetType().Name;
                        _logger.LogWarning("AI provider {From} → {To}, reason: {Reason}", provider.Name, nextName, reason);
                        break;
                    }

                    if (!hasNext) yield break;
                    firstChunkReceived = true;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            if (firstChunkReceived) yield break; // mid-stream fallback yapma
        }

        // Tüm sağlayıcılar başarısız.
        var msg = lastError switch
        {
            AiProviderTimeoutException tex => tex.Message,
            AiProviderUnavailableException uex => uex.Message,
            { } e => e.Message,
            _ => "AI sağlayıcı kullanılamıyor.",
        };
        var code = (lastError as AiProviderTimeoutException)?.Code
                   ?? (lastError as AiProviderUnavailableException)?.Code
                   ?? "provider_unavailable";

        yield return new AiChunk
        {
            Type = "error",
            Code = code,
            Message = msg + " — Ollama'yı başlatın (ollama serve) veya yöneticinize başvurun.",
        };
    }
}
