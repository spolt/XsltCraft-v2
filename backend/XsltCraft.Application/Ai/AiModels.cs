namespace XsltCraft.Application.Ai;

public enum AiTaskKind
{
    RefactorSelection,
    Assistant,
}

public record AssistantMessage(string Role, string Content);

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
    IAsyncEnumerable<AiChunk> StreamAsync(AiRequest req, string prompt, CancellationToken ct);
}
