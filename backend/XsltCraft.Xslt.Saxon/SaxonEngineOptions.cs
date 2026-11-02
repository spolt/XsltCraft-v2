namespace XsltCraft.Xslt.Saxon;

/// <summary>
/// Saxon motorunun profil-bağımlı sınırları. İstek başına sınırlar (süre, çıktı, girdi derinliği/düğümü)
/// <c>XsltExecutionLimits</c>'ten gelir; buradakiler motor örneği ömrü boyunca sabittir.
/// </summary>
public sealed record SaxonEngineOptions
{
    public SaxonProfile Profile { get; init; } = SaxonProfile.Isolated;

    /// <summary>
    /// Template / <c>xsl:function</c> iç içe giriş (trace <c>enter()</c>) sınırı. Yığın bütçesine göre seçilir:
    /// T2.3 ölçümü: 64 MB'ta kırılma ~23.8k template / ~42.2k <c>xsl:function</c> derinliği (ADR 008 Ölçümler).
    /// </summary>
    public int MaxTraceDepth { get; init; } = 1000;

    /// <summary>Saxon <c>Feature.REGEX_BACKTRACKING_LIMIT</c> (ReDoS).</summary>
    public int RegexBacktrackingLimit { get; init; } = 100_000;

    /// <summary>Girdi XML'inde en fazla karakter (<c>XmlReaderSettings.MaxCharactersInDocument</c>).</summary>
    public long MaxInputCharacters { get; init; } = 10_000_000;

    /// <summary>
    /// Stylesheet'te en fazla karakter (derleme öncesi .NET okumasında). Raw rotanın 5M karakterlik XSLT
    /// sınırının üstünde kalmalı; aksi halde servisin kabul ettiği stylesheet worker'da LimitExceeded alır.
    /// </summary>
    public long MaxStylesheetCharacters { get; init; } = 6_000_000;

    /// <summary>Transform başına saklanan en fazla <c>xsl:message</c> sayısı (fazlası sayılır, saklanmaz).</summary>
    public int MaxRetainedMessages { get; init; } = 32;

    /// <summary>Saklanan her mesaj / hata metninin en fazla karakteri.</summary>
    public int MaxMessageChars { get; init; } = 1_024;

    /// <summary>Derleme / çalışma başına saklanan en fazla hata kaydı.</summary>
    public int MaxRetainedErrors { get; init; } = 16;

    public static SaxonEngineOptions Isolated { get; } = new();

    public static SaxonEngineOptions InProcessGrid { get; } = new()
    {
        Profile = SaxonProfile.InProcessGrid,
        MaxTraceDepth = 256,
        MaxInputCharacters = 6_000_000,
        MaxStylesheetCharacters = 1_000_000,
    };

    public static SaxonEngineOptions For(SaxonProfile profile) => profile switch
    {
        SaxonProfile.Isolated => Isolated,
        SaxonProfile.InProcessGrid => InProcessGrid,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Bilinmeyen Saxon profili."),
    };

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxTraceDepth, 1, nameof(MaxTraceDepth));
        ArgumentOutOfRangeException.ThrowIfLessThan(RegexBacktrackingLimit, 1, nameof(RegexBacktrackingLimit));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxInputCharacters, 1, nameof(MaxInputCharacters));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxStylesheetCharacters, 1, nameof(MaxStylesheetCharacters));
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetainedMessages, nameof(MaxRetainedMessages));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxMessageChars, 16, nameof(MaxMessageChars));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRetainedErrors, 1, nameof(MaxRetainedErrors));
    }
}
