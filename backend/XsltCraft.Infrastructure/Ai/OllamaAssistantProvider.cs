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

        List<OllamaMessage> messages;
        if (req.Task == AiTaskKind.Assistant)
        {
            var providerMessages = PromptTemplates.BuildAssistant(req);
            messages = providerMessages
                .Where(m => m.Role != "system")
                .Select(m => new OllamaMessage { Role = m.Role, Content = m.Content })
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

        var payload = new OllamaChatRequest
        {
            Model = ollama.Model,
            Stream = true,
            Messages = messages,
            Options = new OllamaChatOptions { NumPredict = ollama.MaxTokens, NumCtx = ollama.NumCtx },
        };

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
                throw new AiProviderUnavailableException("ollama_model_not_found", $"Ollama model '{ollama.Model}' bulunamadı.");
            throw new AiProviderUnavailableException("ollama_http_error", $"Ollama HTTP {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        using var firstTokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        firstTokenCts.CancelAfter(TimeSpan.FromSeconds(ollama.FirstTokenTimeoutSeconds));

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
                    $"Ollama ilk token {ollama.FirstTokenTimeoutSeconds} sn içinde gelmedi.");
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
                    Model = ollama.Model,
                    Ms = sw.ElapsedMilliseconds,
                };
                yield break;
            }
        }

        response.Dispose();
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage r, CancellationToken ct)
    {
        try { return await r.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = "";
        public bool Stream { get; set; }
        public List<OllamaMessage> Messages { get; set; } = new();
        public OllamaChatOptions? Options { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private sealed class OllamaChatOptions
    {
        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; }
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
