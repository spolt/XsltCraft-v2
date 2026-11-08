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
    /// <summary>Notlar alanına sabit notlar (en fazla 2, her konum bir kez); null → XSLT'deki notlara dokunulmaz.</summary>
    public List<ThemeComposeFixedNoteDto>? FixedNotes { get; set; }
    /// <summary>Yalnız <c>Xslt</c> dalında: her faturada çıkmayabilecek yuvalar (en fazla 32); yalnız uyarı için.</summary>
    public List<string>? ConditionalSlots { get; set; }
    /// <summary>Yalnız <c>Xslt</c> dalında: dosyadaki görsellerin metinde statik taşınması/silinmesi
    /// (en fazla 20, her görsel bir kez). Her çağrıda son analizin döndürdüğü <c>xslt</c> gönderilmeli; compose çıktısı
    /// tekrar girdi yapılmaz; liste kümülatiftir (o analizden bu yana yapılan tüm düzenlemeleri içerir).</summary>
    public List<StaticImageDto>? StaticImages { get; set; }
    /// <summary>Yalnız <c>Xslt</c> dalında: türü olmayan, seçilen yuvaya yerleşen serbest görseller (en fazla 1);
    /// temadaki logo/imzayı gizlemez.</summary>
    public List<ThemeComposeFreeImageDto>? FreeImages { get; set; }
    /// <summary>Yalnız <c>Xslt</c> dalında: belgenin kendi banka satırlarının düzenlenmesi (analizdeki
    /// <c>documentBank.rows[].index</c>; en fazla 20). Liste kümülatiftir; her çağrıda son analizin <c>xslt</c>'si gönderilir.</summary>
    public List<ThemeComposeDocumentBankEditDto>? DocumentBankEdits { get; set; }
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

/// <summary>Serbest görsel: yuva zorunlu; <c>existing-*</c> yalnız temadaki görsel taşınıp/silinip yeri boş kaldıysa kullanılabilir.</summary>
public sealed class ThemeComposeFreeImageDto
{
    public string Slot { get; set; } = "";
    public string DataUrl { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>left | center | right</summary>
    public string Alignment { get; set; } = "left";
}

/// <summary>Belgedeki banka satırına düzenleme.</summary>
public sealed class ThemeComposeDocumentBankEditDto
{
    /// <summary>Analizdeki satır (<c>documentBank.rows[].index</c>); null → yeni satır eklenir.</summary>
    public int? Index { get; set; }
    public string BankName { get; set; } = "";
    /// <summary>TR IBAN (boşluklu ya da boşluksuz).</summary>
    public string Iban { get; set; } = "";
    /// <summary>Satır silinir (<c>Index</c> zorunlu; diğer alanlar yok sayılır).</summary>
    public bool Remove { get; set; }
}

/// <summary>Dosyanın kendi görseli (analizdeki <c>images[].index</c>) için statik düzenleme.</summary>
public sealed class StaticImageDto
{
    /// <summary>Analizdeki görsel sırası (<c>images[].index</c>).</summary>
    public int Index { get; set; }
    /// <summary>Hedef yuva; null → görsel yerinde kalır.</summary>
    public string? Slot { get; set; }
    /// <summary>Görsel metinden silinir (diğer alanlar yok sayılır).</summary>
    public bool Remove { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>left | center | right; null → değişmez.</summary>
    public string? Alignment { get; set; }
}

public sealed class ThemeComposeFixedNoteDto
{
    /// <summary>before | after</summary>
    public string Position { get; set; } = "";
    /// <summary>Düz metin (≤500 karakter, ≤10 satır); satır sonu \n (\r\n de kabul edilir).</summary>
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    /// <summary>9 | 10 | 11 | 12 | 13 | 14 | 16 | 18 (px)</summary>
    public int FontSize { get; set; } = 11;
    /// <summary>#RRGGBB</summary>
    public string Color { get; set; } = "#000000";
    /// <summary>theme | arial | verdana | tahoma | times | georgia | courier</summary>
    public string FontFamily { get; set; } = "theme";
    /// <summary>left | center | right</summary>
    public string Align { get; set; } = "left";
}

public sealed record ThemeComposeWarningDto(string Code, string Message, string? Kind);

public sealed record ThemeComposeResponse(string? Xslt, string? Html, long GenerationTimeMs,
    IReadOnlyList<ThemeComposeWarningDto> Warnings, IReadOnlyDictionary<string, string?> AppliedSlots,
    bool FixedNotesSupported);
