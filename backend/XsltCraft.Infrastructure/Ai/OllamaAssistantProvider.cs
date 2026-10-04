using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;

namespace XsltCraft.Infrastructure.Ai;

public class OllamaAssistantProvider : IAiAssistantProvider
{
    public string Name => "ollama";
    public bool SupportsVision => !string.IsNullOrWhiteSpace(_options.Ollama.VisionModel);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<OllamaAssistantProvider> _logger;

    public OllamaAssistantProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<OllamaAssistantProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest req,
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var ollama = _options.Ollama;
        var client = _httpClientFactory.CreateClient("ollama");

        var payload = BuildPayload(req, prompt, ollama);
        var model = payload.Model; // metin ya da vision modeli — hata/done mesajlarında doğru adı raporla
        var firstTokenTimeout = IsVisionRequest(req) ? ollama.VisionFirstTokenTimeoutSeconds : ollama.FirstTokenTimeoutSeconds;

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(ollama.ConnectTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderTimeoutException("ollama_connect_timeout", "Ollama'ya bağlanılamadı (connect timeout).");
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderUnavailableException("ollama_unavailable", $"Ollama erişilemez: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, ct);
            response.Dispose();
            if ((int)response.StatusCode == 404 || body.Contains("not found", StringComparison.OrdinalIgnoreCase))
                throw new AiProviderUnavailableException("ollama_model_not_found", $"Ollama model '{model}' bulunamadı.");
            throw new AiProviderUnavailableException("ollama_http_error", $"Ollama HTTP {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        using var firstTokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        firstTokenCts.CancelAfter(TimeSpan.FromSeconds(firstTokenTimeout));

        bool firstTokenReceived = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            string? line;
            try
            {
                var readTask = reader.ReadLineAsync(firstTokenReceived ? ct : firstTokenCts.Token).AsTask();
                line = await readTask;
            }
            catch (OperationCanceledException) when (!firstTokenReceived && !ct.IsCancellationRequested)
            {
                response.Dispose();
                throw new AiProviderTimeoutException("ollama_first_token_timeout",
                    $"Ollama ilk token {firstTokenTimeout} sn içinde gelmedi.");
            }

            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ollama satırı parse edilemedi: {Line}", line);
                continue;
            }
            if (parsed is null) continue;

            if (!string.IsNullOrEmpty(parsed.Message?.Content))
            {
                firstTokenReceived = true;
                yield return new AiChunk { Type = "delta", Text = parsed.Message.Content };
            }

            if (parsed.Done)
            {
                yield return new AiChunk
                {
                    Type = "done",
                    Provider = Name,
                    Model = model,
                    Ms = sw.ElapsedMilliseconds,
                };
                yield break;
            }
        }

        response.Dispose();
    }

    private static bool IsVisionRequest(AiRequest req) => req.Task == AiTaskKind.Assistant && req.HasImages;

    /// <summary>
    /// İstek gövdesini kurar (saf; ağ yok) — payload testleri için ayrı. Görselli istek ayrı vision
    /// modeline, kendi bağlam bütçesi/num_ctx/keep_alive değerleriyle gider; metin sohbeti etkilenmez.
    /// </summary>
    internal static OllamaChatRequest BuildPayload(AiRequest req, string prompt, OllamaOptions ollama)
    {
        var vision = IsVisionRequest(req);

        List<OllamaMessage> messages;
        if (req.Task == AiTaskKind.Assistant)
        {
            // Küçük pencere (NumCtx) → özetlenmiş XSLT bütçesi; vision'da görsel token'ları için daha da küçük.
            var providerMessages = PromptTemplates.BuildAssistant(req, vision ? ollama.VisionContextBudget : ollama.ContextBudget);
            messages = providerMessages
                .Where(m => m.Role != "system")
                .Select(m => new OllamaMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    Images = m.Images is { Count: > 0 } ? m.Images.Select(i => i.Base64).ToList() : null,
                })
                .ToList();
            // Ollama'da system mesajı ayrı bir role olarak desteklenir
            var systemMsg = providerMessages.FirstOrDefault(m => m.Role == "system");
            if (systemMsg != null)
                messages.Insert(0, new OllamaMessage { Role = "system", Content = systemMsg.Content });
        }
        else
        {
            messages = [new OllamaMessage { Role = "user", Content = prompt }];
        }

        return new OllamaChatRequest
        {
            Model = vision ? ollama.VisionModel : ollama.Model,
            Stream = true,
            Messages = messages,
            KeepAlive = vision ? ollama.VisionKeepAlive : ollama.KeepAlive,
            Options = new OllamaChatOptions
            {
                NumPredict = ollama.MaxTokens,
                NumCtx = vision ? ollama.VisionNumCtx : ollama.NumCtx,
                NumKeep = ollama.NumKeep,
            },
        };
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage r, CancellationToken ct)
    {
        try { return await r.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal sealed class OllamaChatRequest
    {
        public string Model { get; set; } = "";
        public bool Stream { get; set; }
        public List<OllamaMessage> Messages { get; set; } = new();
        public OllamaChatOptions? Options { get; set; }

        /// <summary>Modelin RAM'de tutulma süresi (örn. "30m"). Cold-start'ı eler.</summary>
        [JsonPropertyName("keep_alive")]
        public string? KeepAlive { get; set; }
    }

    internal sealed class OllamaMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        /// <summary>Base64 görseller (data: öneki yok). Yalnız vision modeline giden son user mesajında dolu.</summary>
        public List<string>? Images { get; set; }
    }

    internal sealed class OllamaChatOptions
    {
        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }
        /// <summary>Context overflow olduğunda promptun başından korunacak token sayısı.</summary>
        [JsonPropertyName("num_keep")]
        public int NumKeep { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaMessage? Message { get; set; }
        public bool Done { get; set; }
    }
}

public class AiProviderTimeoutException : Exception
{
    public string Code { get; }
    public AiProviderTimeoutException(string code, string message) : base(message) => Code = code;
}

public class AiProviderUnavailableException : Exception
{
    public string Code { get; }
    public AiProviderUnavailableException(string code, string message, Exception? inner = null) : base(message, inner) => Code = code;
}
