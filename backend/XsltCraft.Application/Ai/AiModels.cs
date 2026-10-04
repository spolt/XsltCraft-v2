namespace XsltCraft.Application.Ai;

public enum AiTaskKind
{
    RefactorSelection,
    Assistant,
}

/// <param name="ImageCount">Bu geçmiş mesaja eklenmiş görsel sayısı. Görseller yeniden gönderilmez; prompt'ta yer tutucu olarak geçer.</param>
public record AssistantMessage(string Role, string Content, int ImageCount = 0);

/// <summary>
/// Doğrulanmış (magic-byte + boyut) ve kanonik base64'e çevrilmiş görsel. Sağlayıcılar base64 istediği
/// için byte yerine base64 taşınır (ikinci kodlama yok). Geçicidir: DB/storage/log'a yazılmaz.
/// </summary>
public record AiImageInput(string MimeType, string Base64, int Width, int Height, int ByteLength);

public class AiRequest
{
    public AiTaskKind Task { get; set; }
    public string? UserXml { get; set; }
    public string? UserXslt { get; set; }
    public string? UserRequest { get; set; }
    public string? Selection { get; set; }
    public string? XmlSelection { get; set; }
    /// <summary>XSLT editöründe imlecin bulunduğu satır (1-tabanlı); template alaka skorunda boost için.</summary>
    public int? XsltCursorLine { get; set; }
    public List<AssistantMessage>? History { get; set; }
    /// <summary>Geçmiş başarılı örnekler (few-shot). Prompt'ta system'a değil ilk user bağlamına enjekte edilir.</summary>
    public List<AiExemplar>? Exemplars { get; set; }
    /// <summary>Yalnız mevcut turun görselleri (doğrulanmış). Null/boş = metin-only istek.</summary>
    public List<AiImageInput>? Images { get; set; }
    public bool HasImages => Images is { Count: > 0 };
    public int MaxTokens { get; set; } = 2048;
}

/// <summary>Kullanıcının geçmişte işine yaramış bir soru→cevap örneği (few-shot exemplar).</summary>
public record AiExemplar(string Question, string Answer);

/// <summary>
/// Sağlayıcıya özel bağlam bütçesi. Küçük pencere (Ollama 3b, 8K token) özetlenmiş XSLT alır;
/// büyük pencere (Gemini 1M token) TAM .xslt dosyasını ham alır — model şablonun tamamını bilir.
/// </summary>
/// <param name="RawXsltThresholdChars">Bu boyuta kadar XSLT özetlenmeden HAM gönderilir.</param>
/// <param name="XsltLimitChars">XSLT bloğunun (ham ya da özet) üst sınırı; aşarsa baş/son kırpılır.</param>
/// <param name="XmlLimitChars">XML bloğunun üst sınırı.</param>
public record AiContextBudget(int RawXsltThresholdChars, int XsltLimitChars, int XmlLimitChars)
{
    /// <summary>Varsayılan: bugüne kadarki davranış (Ollama'ya göre ayarlı).</summary>
    public static readonly AiContextBudget Default = new(
        RawXsltThresholdChars: 6_000,
        XsltLimitChars: 16_000,
        XmlLimitChars: 8_000);
}

public class AiChunk
{
    public string Type { get; set; } = "delta"; // "delta" | "done" | "error"
    public string? Text { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public long? Ms { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public interface IAiAssistantProvider
{
    string Name { get; }
    /// <summary>Görsel girdiyi işleyebilir mi (Gemini: evet; Ollama: yalnız VisionModel tanımlıysa).</summary>
    bool SupportsVision { get; }
    IAsyncEnumerable<AiChunk> StreamAsync(AiRequest req, string prompt, CancellationToken ct);
}
