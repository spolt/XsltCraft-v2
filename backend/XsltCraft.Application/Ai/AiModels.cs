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
