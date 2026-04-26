namespace XsltCraft.Application.Ai;

public class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; }
    public OllamaOptions Ollama { get; set; } = new();
    public GeminiOptions Gemini { get; set; } = new();
    /// <summary>0 = sınırsız. Yaklaşık token hesabı: üretilen karakter sayısı / 4.</summary>
    public int DailyTokenBudgetPerUser { get; set; } = 50_000;
    /// <summary>"auto" | "ollama" | "gemini". DB FeatureFlag (ai.preferred_provider) öncelikli.</summary>
    public string PreferredProvider { get; set; } = "auto";
}

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:7b";
    public int FirstTokenTimeoutSeconds { get; set; } = 8;
    public int ConnectTimeoutSeconds { get; set; } = 3;
    public int MaxTokens { get; set; } = 4096;
}

public class GeminiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
    public int MaxTokens { get; set; } = 2048;
    /// <summary>Cloud için Ollama'dan daha cömert default; ağ üzerinden TLS handshake var.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;
    /// <summary>Cloud için ilk token genelde 1-3 sn'de gelir; soğuk başlangıç + retry payı bırakır.</summary>
    public int FirstTokenTimeoutSeconds { get; set; } = 15;
}
