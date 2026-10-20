namespace XsltCraft.Application.DTO;

/// <summary>
/// Hazır temaya kullanıcı logo/imza/IBAN bilgisini ekleyen istek. <c>ThemeId</c> (Tema Kullan) ya da <c>Xslt</c>
/// (Geliştirici Modu — editördeki metin) verilir, ikisi birden değil.
/// </summary>
public sealed class ThemeComposeRequest
{
    public Guid? ThemeId { get; set; }
    public string? Xslt { get; set; }
    public string? XmlContent { get; set; }
    public List<ThemeComposeAssetDto> Assets { get; set; } = [];
    public List<BankInfoItem> BankInfo { get; set; } = [];
    /// <summary>Kullanıcının görsel eklemeden kaldırdığı temadaki görseller: logo | signature (admin izin verdiyse uygulanır).</summary>
    public List<string> HideThemeImages { get; set; } = [];
    public bool IncludeXslt { get; set; }
    public bool IncludeHtml { get; set; }
}

public sealed class ThemeComposeAssetDto
{
    /// <summary>logo | signature</summary>
    public string Kind { get; set; } = "";
    /// <summary>Yuva kimliği; null → temanın varsayılanı.</summary>
    public string? Slot { get; set; }
    public string DataUrl { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>left | center | right</summary>
    public string Alignment { get; set; } = "left";
}

public sealed record ThemeComposeWarningDto(string Code, string Message, string? Kind);

public sealed record ThemeComposeResponse(string? Xslt, string? Html, long GenerationTimeMs,
    IReadOnlyList<ThemeComposeWarningDto> Warnings, IReadOnlyDictionary<string, string?> AppliedSlots);
