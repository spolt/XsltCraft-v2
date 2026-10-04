using System.Text.Json;

using XsltCraft.Application.Ai;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Infrastructure.Tests.Ai;

/// <summary>
/// Sağlayıcıya giden JSON gövdesi (ağsız). Görselli istekte görsel yalnız son user mesajında olmalı;
/// metin-only istekte görsel alanı hiç yazılmamalı ve model/ayarlar değişmemeli.
/// </summary>
public class ProviderPayloadTests
{
    private static readonly AiImageInput Image = new("image/png", "iVBORw0KGgo=", 1536, 864, 8);

    private static AiRequest Request(bool withImage) => new()
    {
        Task = AiTaskKind.Assistant,
        UserRequest = "toplamları sağa hizala",
        UserXslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"/>",
        History =
        [
            new AssistantMessage("user", "önceki soru", ImageCount: 1),
            new AssistantMessage("assistant", "önceki cevap"),
        ],
        Images = withImage ? [Image] : null,
    };

    // ── Gemini ───────────────────────────────────────────────────────────────

    private static JsonElement Gemini(AiRequest req)
    {
        var payload = GeminiAssistantProvider.BuildPayload(req, prompt: "", new GeminiOptions());
        return JsonDocument.Parse(JsonSerializer.Serialize(payload, GeminiAssistantProvider.JsonOptions)).RootElement;
    }

    [Fact]
    public void Gemini_WithImage_InlineDataBeforeText_InLastUserTurnOnly()
    {
        var root = Gemini(Request(withImage: true));

        Assert.True(root.TryGetProperty("system_instruction", out _));
        var contents = root.GetProperty("contents").EnumerateArray().ToList();
        var last = contents[^1];
        Assert.Equal("user", last.GetProperty("role").GetString());

        var parts = last.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(2, parts.Count);

        var inline = parts[0].GetProperty("inline_data");
        Assert.Equal("image/png", inline.GetProperty("mime_type").GetString());
        Assert.Equal(Image.Base64, inline.GetProperty("data").GetString());
        Assert.False(parts[0].TryGetProperty("text", out _)); // "text":"" gönderilirse Gemini 400 döner
        Assert.Contains("toplamları sağa hizala", parts[1].GetProperty("text").GetString());

        foreach (var earlier in contents[..^1])
            foreach (var part in earlier.GetProperty("parts").EnumerateArray())
                Assert.False(part.TryGetProperty("inline_data", out _));
    }

    [Fact]
    public void Gemini_TextOnly_HasNoInlineData()
    {
        var json = JsonSerializer.Serialize(
            GeminiAssistantProvider.BuildPayload(Request(withImage: false), "", new GeminiOptions()),
            GeminiAssistantProvider.JsonOptions);

        Assert.DoesNotContain("inline_data", json);
    }

    [Fact]
    public void Gemini_SupportsVision_OnlyWithApiKey()
    {
        static bool Supports(string key) => new GeminiAssistantProvider(
            null!, Microsoft.Extensions.Options.Options.Create(new AiOptions { Gemini = new GeminiOptions { ApiKey = key } }),
            null!).SupportsVision;

        Assert.True(Supports("key"));
        Assert.False(Supports(""));
    }

    // ── Ollama ───────────────────────────────────────────────────────────────

    private static readonly OllamaOptions OllamaCfg = new()
    {
        Model = "qwen2.5-coder:3b",
        VisionModel = "qwen2.5vl:3b",
        KeepAlive = "30m",
        VisionKeepAlive = "10m",
        NumCtx = 32_768,
        VisionNumCtx = 16_384,
    };

    private static JsonElement Ollama(AiRequest req, OllamaOptions? cfg = null)
    {
        var payload = OllamaAssistantProvider.BuildPayload(req, prompt: "", cfg ?? OllamaCfg);
        return JsonDocument.Parse(JsonSerializer.Serialize(payload, OllamaAssistantProvider.JsonOptions)).RootElement;
    }

    [Fact]
    public void Ollama_WithImage_UsesVisionModelAndSettings_ImagesOnLastUser()
    {
        var root = Ollama(Request(withImage: true));

        Assert.Equal("qwen2.5vl:3b", root.GetProperty("model").GetString());
        Assert.Equal("10m", root.GetProperty("keep_alive").GetString());
        Assert.Equal(16_384, root.GetProperty("options").GetProperty("num_ctx").GetInt32());

        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());

        var last = messages[^1];
        Assert.Equal("user", last.GetProperty("role").GetString());
        Assert.Equal([Image.Base64], last.GetProperty("images").EnumerateArray().Select(e => e.GetString()));

        foreach (var earlier in messages[..^1])
            Assert.False(earlier.TryGetProperty("images", out _));
    }

    [Fact]
    public void Ollama_TextOnly_KeepsTextModel_AndNoImagesField()
    {
        var root = Ollama(Request(withImage: false));

        Assert.Equal("qwen2.5-coder:3b", root.GetProperty("model").GetString());
        Assert.Equal("30m", root.GetProperty("keep_alive").GetString());
        Assert.Equal(32_768, root.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.DoesNotContain("\"images\"", root.GetRawText());
    }

    [Fact]
    public void Ollama_WithImage_UsesSmallerVisionContextBudget()
    {
        // Prod benzeri: metin modeli 64K'ya kadar ham XSLT görür; vision bütçesi 6K ham / 12K üst sınır.
        var cfg = new OllamaOptions
        {
            VisionModel = "qwen2.5vl:3b",
            RawXsltThresholdChars = 64_000,
            MaxXsltChars = 64_000,
        };
        var marker = string.Concat(Enumerable.Repeat("<!-- dolgu -->", 1_500)); // ~21K karakter
        var xslt = "<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
                   + marker + "</xsl:stylesheet>";

        var textReq = Request(withImage: false);
        textReq.UserXslt = xslt;
        var visionReq = Request(withImage: true);
        visionReq.UserXslt = xslt;

        var textUser = Ollama(textReq, cfg).GetProperty("messages")[1].GetProperty("content").GetString()!;
        var visionUser = Ollama(visionReq, cfg).GetProperty("messages")[1].GetProperty("content").GetString()!;

        Assert.Contains(xslt, textUser);                                  // metin: tam dosya ham
        Assert.DoesNotContain(xslt, visionUser);                          // vision: özet/kırpılmış
        Assert.True(visionUser.Length <= cfg.VisionMaxXsltChars + 200);   // 12K sınırı (+etiketler)
    }
}
