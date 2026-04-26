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
    public List<AssistantMessage>? History { get; set; }
    public int MaxTokens { get; set; } = 2048;
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
