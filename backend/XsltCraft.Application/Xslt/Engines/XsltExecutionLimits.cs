namespace XsltCraft.Application.Xslt.Engines;

/// <summary>Tek bir derleme/dönüşüm için kaynak sınırları.</summary>
public sealed record XsltExecutionLimits
{
    public static XsltExecutionLimits Default { get; } = new();

    /// <summary>
    /// Dönüşüm süre sınırı. Asıl garanti host tarafındadır (worker süreci öldürülür); motorlar
    /// ayrıca işbirlikçi olarak kontrol eder.
    /// </summary>
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Üretilebilecek en fazla çıktı karakteri.</summary>
    public int MaxOutputChars { get; init; } = 8_000_000;

    /// <summary>Stylesheet'te en fazla eleman iç içeliği.</summary>
    public int MaxStylesheetDepth { get; init; } = 256;

    /// <summary>Girdi XML'inde en fazla eleman iç içeliği.</summary>
    public int MaxInputDepth { get; init; } = 512;

    /// <summary>Girdi XML'inde en fazla düğüm (eleman, öznitelik, metin vb.) sayısı.</summary>
    public int MaxInputNodes { get; init; } = 1_000_000;
}
