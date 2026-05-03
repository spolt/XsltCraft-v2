using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;

namespace XsltCraft.Infrastructure.Ai;

public interface IAiProviderHealthService
{
    Task<IReadOnlyList<AiProviderHealth>> CheckAsync(CancellationToken ct = default);
}

public record AiProviderHealth(
    string Name,
    bool Configured,
    bool Available,
    string? Model,
    long? LatencyMs,
    string? Error);

/// <summary>
/// Admin paneli için sağlayıcı durumu — Ollama'ya gerçek bağlantı atar (kısa timeout),
/// Gemini için yalnızca konfigürasyon kontrolü yapar (API çağrısı = kota harcar).
/// </summary>
public class AiProviderHealthService : IAiProviderHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<AiProviderHealthService> _logger;

    public AiProviderHealthService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<AiProviderHealthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiProviderHealth>> CheckAsync(CancellationToken ct = default)
    {
        var ollamaTask = CheckOllamaAsync(ct);
        var geminiTask = Task.FromResult(CheckGemini());
        await Task.WhenAll(ollamaTask, geminiTask);
        return new[] { ollamaTask.Result, geminiTask.Result };
    }

    private async Task<AiProviderHealth> CheckOllamaAsync(CancellationToken ct)
    {
        var ollama = _options.Ollama;
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, ollama.ConnectTimeoutSeconds)));

            var client = _httpClientFactory.CreateClient("ollama");
            using var res = await client.GetAsync("/api/tags", cts.Token);
            if (!res.IsSuccessStatusCode)
                return new AiProviderHealth("ollama", true, false, ollama.Model, sw.ElapsedMilliseconds,
                    $"HTTP {(int)res.StatusCode}");

            var body = await res.Content.ReadAsStringAsync(ct);
            var modelExists = false;
            try
            {
                var parsed = JsonSerializer.Deserialize<TagsResponse>(body);
                modelExists = parsed?.Models?.Any(m =>
                    string.Equals(m.Name, ollama.Model, StringComparison.OrdinalIgnoreCase) ||
                    (m.Name?.StartsWith(ollama.Model.Split(':')[0], StringComparison.OrdinalIgnoreCase) ?? false)
                ) ?? false;
            }
            catch { /* parse hatası model kontrolünü atlar */ }

            return new AiProviderHealth(
                Name: "ollama",
                Configured: true,
                Available: true,
                Model: ollama.Model,
                LatencyMs: sw.ElapsedMilliseconds,
                Error: modelExists ? null : $"Model '{ollama.Model}' yüklü değil (ollama pull {ollama.Model}).");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama healthcheck başarısız.");
            return new AiProviderHealth("ollama", true, false, ollama.Model, sw.ElapsedMilliseconds,
                $"Ollama erişilemez: {ex.Message}");
        }
    }

    private AiProviderHealth CheckGemini()
    {
        var cfg = _options.Gemini;
        if (!cfg.Enabled)
            return new AiProviderHealth("gemini", false, false, cfg.Model, null,
                "Devre dışı (Ai:Gemini:Enabled=false).");
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return new AiProviderHealth("gemini", true, false, cfg.Model, null,
                "API key tanımlı değil.");
        // Gerçek ping yapılmıyor — her healthcheck'te kota yakmamak için.
        return new AiProviderHealth("gemini", true, true, cfg.Model, null, null);
    }

    private sealed class TagsResponse
    {
        [JsonPropertyName("models")] public List<TagModel>? Models { get; set; }
    }
    private sealed class TagModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
