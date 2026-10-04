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

    /// <summary>
    /// "auto" modda XSLT bu boyutu (karakter) aşarsa istek Gemini'ye öncelikli yönlenir —
    /// Gemini tam dosyayı gördüğü için büyük şablonlarda doğru cevap verir; Ollama yedek kalır.
    /// Varsayılan 64K = Ollama'nın NumCtx=32768 ile ham görebildiği üst sınırla hizalı.
    /// 0 = kapalı (her zaman Ollama önce). Gemini kayıtlı değilse etkisizdir.
    /// </summary>
    public int LargeXsltGeminiThresholdChars { get; set; } = 64_000;

    public VisionOptions Vision { get; set; } = new();
}

/// <summary>
/// Sohbete ekran görüntüsü (vision) desteği. Görseller geçicidir: DB/storage'a yazılmaz, loglanmaz.
/// Plan bazlı mesaj başına limit Membership'te (MaxAiImagesPerMessage); buradaki değerler teknik tavandır.
/// </summary>
public class VisionOptions
{
    /// <summary>Varsayılan kapalı. DB FeatureFlag (ai.vision_enabled) öncelikli.</summary>
    public bool Enabled { get; set; }
    /// <summary>Tercih "ollama" iken yerel vision modeli yoksa/çökerse Gemini'ye izin verilsin mi. DB FeatureFlag (ai.vision_gemini_fallback) öncelikli.</summary>
    public bool GeminiFallback { get; set; } = true;
    /// <summary>Mesaj başına teknik tavan (plan limiti bunu aşamaz).</summary>
    public int MaxImagesPerMessage { get; set; } = 3;
    /// <summary>Decode edilmiş görsel başına üst sınır (bayt).</summary>
    public int MaxImageBytes { get; set; } = 1_572_864;
    /// <summary>Mesajdaki tüm görsellerin decode edilmiş toplamı (bayt).</summary>
    public int MaxTotalBytes { get; set; } = 4_194_304;
    /// <summary>Uzun kenar üst sınırı (piksel). İstemci 1536'ya küçültür; bu sunucu tavanı.</summary>
    public int MaxLongEdgePx { get; set; } = 2048;
    /// <summary>Toplam piksel üst sınırı (decompression bomb koruması).</summary>
    public int MaxPixels { get; set; } = 4_200_000;
    /// <summary>Kotaya görsel başına eklenen yaklaşık token (girdi maliyeti). Yalnız çıktı üretildiyse eklenir.</summary>
    public int TokenCostPerImage { get; set; } = 1000;
    /// <summary>Kullanıcı başına dakikada görselli istek sınırı.</summary>
    public int PerUserPerMinute { get; set; } = 10;
}

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:3b";
    /// <summary>Yerel model cold-start (RAM'e yükleme) süresini hesaba kat; sıcakken ilk token ~1-2 sn.</summary>
    public int FirstTokenTimeoutSeconds { get; set; } = 30;
    public int ConnectTimeoutSeconds { get; set; } = 3;
    public int MaxTokens { get; set; } = 4096;
    /// <summary>Ollama context window (token). Sistem promptu ~3-4K token; 2048 varsayılan bunu keser.</summary>
    public int NumCtx { get; set; } = 8192;

    /// <summary>
    /// Context taşınca prompt'un BAŞINDAN korunacak token sayısı (Identity + Constraints sığsın).
    /// Identity ~600, Constraints ~150 → 1024 güvenli üst sınır.
    /// </summary>
    public int NumKeep { get; set; } = 1024;

    /// <summary>
    /// Modelin RAM'de tutulma süresi. Cold-start'ı eler — istek aralıkları kısaysa "30m" idealdir.
    /// Ollama formatları: "5m", "30m", "1h", "-1" (sınırsız), "0" (yükle, hemen boşalt).
    /// </summary>
    public string KeepAlive { get; set; } = "30m";

    /// <summary>Bu boyuta (karakter) kadar XSLT özetlenmeden ham gönderilir. NumCtx büyütülürse artırılabilir.</summary>
    public int RawXsltThresholdChars { get; set; } = 6_000;
    /// <summary>XSLT bloğu üst sınırı (karakter). ~4 karakter ≈ 1 token; NumCtx=8192 ile 16K güvenli.</summary>
    public int MaxXsltChars { get; set; } = 16_000;
    /// <summary>XML bloğu üst sınırı (karakter).</summary>
    public int MaxXmlChars { get; set; } = 8_000;

    public AiContextBudget ContextBudget => new(RawXsltThresholdChars, MaxXsltChars, MaxXmlChars);

    /// <summary>
    /// Görselli mesajlar için ayrı vision modeli (ör. "qwen2.5vl:3b"). Boş = Ollama görsel desteklemez.
    /// Metin sohbeti her zaman <see cref="Model"/>'de kalır.
    /// </summary>
    public string VisionModel { get; set; } = string.Empty;
    /// <summary>Vision modelinin RAM'de tutulma süresi; metin modelinden kısa tutulur.</summary>
    public string VisionKeepAlive { get; set; } = "10m";
    /// <summary>Görsel kodlama (CPU) + prefill yavaş; ilk token için daha uzun süre.</summary>
    public int VisionFirstTokenTimeoutSeconds { get; set; } = 120;
    /// <summary>Vision context window. Sabit kalmalı — değişirse Ollama modeli yeniden yükler.</summary>
    public int VisionNumCtx { get; set; } = 16_384;
    public int VisionRawXsltThresholdChars { get; set; } = 6_000;
    public int VisionMaxXsltChars { get; set; } = 12_000;
    public int VisionMaxXmlChars { get; set; } = 6_000;

    public AiContextBudget VisionContextBudget => new(VisionRawXsltThresholdChars, VisionMaxXsltChars, VisionMaxXmlChars);
}

public class GeminiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
    public int MaxTokens { get; set; } = 4096;
    /// <summary>Cloud için Ollama'dan daha cömert default; ağ üzerinden TLS handshake var.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;
    /// <summary>Cloud için ilk token genelde 1-3 sn'de gelir; soğuk başlangıç + retry payı bırakır.</summary>
    public int FirstTokenTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Gemini 2.5 Flash 1M token pencereye sahip → tipik GİB şablonu (~250KB) TAM ve HAM gönderilir;
    /// model şablonun tamamını bilir. 400K karakter ≈ ~100K token, güvenli sınır.
    /// </summary>
    public int RawXsltThresholdChars { get; set; } = 400_000;
    public int MaxXsltChars { get; set; } = 400_000;
    public int MaxXmlChars { get; set; } = 100_000;

    public AiContextBudget ContextBudget => new(RawXsltThresholdChars, MaxXsltChars, MaxXmlChars);
}
