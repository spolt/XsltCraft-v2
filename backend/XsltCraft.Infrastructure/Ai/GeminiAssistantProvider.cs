using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;

namespace XsltCraft.Infrastructure.Ai;

/// <summary>
/// Google Generative Language API (Gemini) — streamGenerateContent SSE.
/// Yalnızca Ai:Gemini:Enabled=true ise DI'a kaydedilir.
/// </summary>
public class GeminiAssistantProvider : IAiAssistantProvider
{
    public string Name => "gemini";

    private const string BaseEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiAssistantProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GeminiAssistantProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<GeminiAssistantProvider> logger)
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
        var cfg = _options.Gemini;
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new AiProviderUnavailableException("gemini_no_key", "Gemini API key tanımlı değil.");

        var client = _httpClientFactory.CreateClient("gemini");

        GeminiRequest payload;
        if (req.Task == AiTaskKind.Assistant)
        {
            var providerMessages = PromptTemplates.BuildAssistant(req);
            var systemMsg = providerMessages.FirstOrDefault(m => m.Role == "system");
            var contents = providerMessages
                .Where(m => m.Role != "system")
                .Select(m => new GeminiContent
                {
                    Role = m.Role == "assistant" ? "model" : "user",
                    Parts = [new GeminiPart { Text = m.Content }],
                })
                .ToList();

            payload = new GeminiRequest
            {
                SystemInstruction = systemMsg is null ? null : new GeminiContent
                {
                    Parts = [new GeminiPart { Text = systemMsg.Content }],
                },
                Contents = contents,
                GenerationConfig = new GeminiGenerationConfig { MaxOutputTokens = req.MaxTokens },
            };
        }
        else
        {
            payload = new GeminiRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = prompt }],
                    },
                ],
                GenerationConfig = new GeminiGenerationConfig { MaxOutputTokens = req.MaxTokens },
            };
        }

        var url = $"{BaseEndpoint}/{Uri.EscapeDataString(cfg.Model)}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(cfg.ApiKey)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(cfg.ConnectTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderTimeoutException("gemini_connect_timeout", "Gemini'ye bağlanılamadı.");
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderUnavailableException("gemini_unavailable", $"Gemini erişilemez: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            throw new AiProviderUnavailableException("gemini_http_error",
                $"Gemini HTTP {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        using var firstTokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        firstTokenCts.CancelAfter(TimeSpan.FromSeconds(cfg.FirstTokenTimeoutSeconds));

        bool firstTokenReceived = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(firstTokenReceived ? ct : firstTokenCts.Token);
            }
            catch (OperationCanceledException) when (!firstTokenReceived && !ct.IsCancellationRequested)
            {
                response.Dispose();
                throw new AiProviderTimeoutException("gemini_first_token_timeout",
                    "Gemini ilk token süresinde gelmedi.");
            }

            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var json = line[5..].Trim();
            if (json.Length == 0) continue;

            GeminiStreamChunk? evt;
            try { evt = JsonSerializer.Deserialize<GeminiStreamChunk>(json); }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Gemini SSE parse hatası: {Line}", line);
                continue;
            }
            if (evt is null) continue;

            var candidate = evt.Candidates?.FirstOrDefault();
            if (candidate is null) continue;

            var text = candidate.Content?.Parts?
                .Select(p => p.Text)
                .Where(t => !string.IsNullOrEmpty(t))
                .Aggregate(string.Empty, (a, b) => a + b);

            if (!string.IsNullOrEmpty(text))
            {
                firstTokenReceived = true;
                yield return new AiChunk { Type = "delta", Text = text };
            }

            if (!string.IsNullOrEmpty(candidate.FinishReason))
            {
                if (candidate.FinishReason == "MAX_TOKENS")
                    yield return new AiChunk { Type = "delta", Text = "\n\n_[Cevap max_tokens limitine ulaştı.]_" };

                yield return new AiChunk
                {
                    Type = "done",
                    Provider = Name,
                    Model = cfg.Model,
                    Ms = sw.ElapsedMilliseconds,
                };
                response.Dispose();
                yield break;
            }
        }

        response.Dispose();
    }

    private sealed class GeminiRequest
    {
        [JsonPropertyName("system_instruction")] public GeminiContent? SystemInstruction { get; set; }
        [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; set; } = new();
        [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; set; } = new();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiStreamChunk
    {
        [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
        [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
    }
}
