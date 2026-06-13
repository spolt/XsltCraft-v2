namespace XsltCraft.Application.DTO;

public sealed class UpdateUserXsltRequest
{
    public string? Name { get; set; }
    public string? XsltContent { get; set; }
    public string? XmlContent { get; set; }

    // İyimser çakışma kontrolü: istemci yüklediği sürümün UpdatedAt'ini gönderir.
    // DB'deki değer daha yeniyse kaydetme reddedilir (409). DateTimeOffset, timezone
    // farklarından kaynaklı yanlış çakışmaları önler.
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
}
