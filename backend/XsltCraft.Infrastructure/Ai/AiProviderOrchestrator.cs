using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;

namespace XsltCraft.Infrastructure.Ai;

/// <summary>
/// Deterministik fallback: varsayılan Ollama → Gemini, tercih "gemini" ise Gemini → Ollama.
/// Mid-stream fallback yok — ilk chunk geldikten sonra hata olursa kullanıcıya hata chunk'ı gönderilir.
/// Görselli istekte sıra <see cref="IAiVisionAvailability"/>'den gelir (yalnız görseli işleyebilen sağlayıcılar).
/// </summary>
public class AiProviderOrchestrator
{
    private readonly OllamaAssistantProvider _ollama;
    private readonly IReadOnlyList<IAiAssistantProvider> _others; // Ollama dışı sağlayıcılar
    private readonly IAiFeatureFlagService _flagService;
    private readonly IAiVisionAvailability _vision;
    private readonly AiOptions _options;
    private readonly ILogger<AiProviderOrchestrator> _logger;

    public AiProviderOrchestrator(
        OllamaAssistantProvider ollama,
        IEnumerable<IAiAssistantProvider> allProviders,
        IAiFeatureFlagService flagService,
        IAiVisionAvailability vision,
        IOptions<AiOptions> options,
        ILogger<AiProviderOrchestrator> logger)
    {
        _ollama = ollama;
        _others = allProviders.Where(p => p.Name != ollama.Name).ToList();
        _flagService = flagService;
        _vision = vision;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest req,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var prompt = PromptTemplates.Build(req);

        var providers = req.HasImages
            ? await ResolveVisionProvidersAsync(ct)
            : await ResolveTextProvidersAsync(req, ct);

        if (providers.Count == 0)
        {
            // Gate bunu zaten 400 ile keser; burası yarış durumu (bayrak arada kapandı) için güvenlik ağı.
            yield return new AiChunk
            {
                Type = "error",
                Code = AiVisionGate.CodeUnavailable,
                Message = "Ekran görüntüsü analizi için uygun AI sağlayıcısı yok.",
            };
            yield break;
        }

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
        var baseMsg = lastError switch
        {
            AiProviderTimeoutException tex => tex.Message,
            AiProviderUnavailableException uex => uex.Message,
            { } e => e.Message,
            _ => "AI sağlayıcı kullanılamıyor.",
        };
        var code = (lastError as AiProviderTimeoutException)?.Code
                   ?? (lastError as AiProviderUnavailableException)?.Code
                   ?? "provider_unavailable";

        // Hata koduna göre kullanıcıya yardımcı ipucu seç. Cold-start timeout'larında "Ollama'yı başlatın"
        // önerisi yanıltıcı — servis zaten ayakta, model henüz ısınıyor.
        var hint = code switch
        {
            "ollama_first_token_timeout" => " — Yerel model ısınıyor olabilir, birkaç saniye sonra tekrar deneyin.",
            "ollama_connect_timeout" or "ollama_unavailable" => " — Ollama'yı başlatın (ollama serve) veya yöneticinize başvurun.",
            "ollama_model_not_found" => " — Modeli indirin: 'ollama pull <model>'.",
            _ => string.Empty,
        };

        yield return new AiChunk
        {
            Type = "error",
            Code = code,
            Message = baseMsg + hint,
        };
    }

    private async Task<List<IAiAssistantProvider>> ResolveTextProvidersAsync(AiRequest req, CancellationToken ct)
    {
        var preferred = await _flagService.GetStringAsync(AiFlagKeys.PreferredProvider, ct)
                        ?? _options.PreferredProvider;

        // "auto" modda büyük XSLT → Gemini öncelikli (tam dosyayı görür); açık tercih kazanır.
        var effective = ProviderRouting.Resolve(
            preferred, req.Task, req.UserXslt?.Length ?? 0, _options.LargeXsltGeminiThresholdChars);
        if (effective == "gemini" && preferred != "gemini")
            _logger.LogInformation(
                "Büyük XSLT ({Len} kr) → Gemini öncelikli yönlendirme (auto).", req.UserXslt?.Length ?? 0);

        // "gemini" öncelikli: Gemini varsa önce dene, Ollama yedek.
        return effective == "gemini" && _others.Count > 0
            ? [.. _others, _ollama]
            : [(IAiAssistantProvider)_ollama, .. _others];
    }

    private async Task<List<IAiAssistantProvider>> ResolveVisionProvidersAsync(CancellationToken ct)
    {
        IAiAssistantProvider[] all = [_ollama, .. _others];
        var names = await _vision.ResolveProvidersAsync(ct);
        return names
            .Select(name => all.FirstOrDefault(p => p.Name == name))
            .OfType<IAiAssistantProvider>()
            .ToList();
    }
}
