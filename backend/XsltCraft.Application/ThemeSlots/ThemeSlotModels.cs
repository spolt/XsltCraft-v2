namespace XsltCraft.Application.ThemeSlots;

/// <summary>Kullanıcının temaya yerleştirebildiği görsel türü.</summary>
public enum ThemeAssetKind { Logo, Signature }

public enum ThemeAlignment { Left, Center, Right }

/// <summary>Görselin yuvaya yerleşim biçimi (runtime betiği <c>data-xc-layout</c> niteliğinden okur).</summary>
public enum SlotLayout { Block, BlockClear, FloatLeft }

/// <summary>Analiz sonucu: Ok = çekirdek çapalar bulundu ve hiçbir aday düşmedi; Partial = en az bir yuva;
/// None = hiç yuva yok; Failed = analiz/doğrulama başarısız (tema işaretsiz saklanır); Skipped = analiz yapılmadı.</summary>
public enum ThemeSlotStatus { Ok, Partial, None, Failed, Skipped }

/// <param name="Id">Kararlı yuva kimliği (<c>^[a-z][a-z0-9-]{1,40}\z</c>), XSLT'de <c>data-xc-slot</c> değeri.</param>
/// <param name="Rule">Yuvayı üreten kural (R1–R9).</param>
/// <param name="LegacyId">Yuva mevcut bir tutucuyu (<c>#company_logo</c> vb.) benimsediyse onun id'si.</param>
/// <param name="Inserted">true → boş işaret elemanı eklendi; false → mevcut tutucuya yalnız nitelik eklendi.</param>
/// <param name="Line">Yamalı XSLT'deki yaklaşık satır (rapor için).</param>
/// <param name="Removable">Temadaki görsel yuvası ve kullanıcı görseli kaldırabilir.</param>
public sealed record ThemeSlot(
    string Id, string Rule, int Order, SlotLayout Layout, ThemeAssetKind? DefaultFor,
    bool Conditional, string? LegacyId, bool Inserted, int Line, bool Removable = false);

public sealed record ThemeAnchorResult(string Id, bool Found, int? Line, string? Via);

public sealed record ThemeSlotWarning(string Code, string Message, string? SlotId = null);

public sealed record OverridesSummary(bool Found, string? Decision, int ImagesKept, int BankRowsKept);

/// <summary>Temaya gömülü statik görselin rolü — admin yüklemede seçer (öneri analizden gelir).
/// Logo/İmza: görselin kendisi yuva olur; kullanıcı kendi görselini eklediğinde onun yerini alır.
/// Tasarım: dokunulmaz.</summary>
public enum ThemeImageRole { Design, Logo, Signature }

/// <summary>Analizin bulduğu, yuva yapılabilecek statik görsel (yükleme kararı için).</summary>
/// <param name="Index">Belge sırasındaki konumu — kararın hangi görsele ait olduğu bununla eşleşir.</param>
/// <param name="Reason">Önerinin gerekçesi (admin'e gösterilir).</param>
/// <param name="PreviewDataUrl">Küçük resim için; yalnız <c>data:image/</c> kaynakları (dış adres istenmez).</param>
public sealed record ThemeImageInfo(int Index, ThemeImageRole Suggested, string Reason, int Line, string? PreviewDataUrl);

/// <summary>Admin'in bir görsel için kararı.</summary>
/// <param name="Removable">Kullanıcı kendi görselini eklemeden temadaki görseli kaldırabilir (yalnız logo/imza).</param>
/// <param name="Strip">Admin görseli yüklemede temadan kaldırır: tasarım görseli silinir; logo/imzanın yalnız içeriği
/// silinir, yeri kullanıcının görseli için konum olarak kalır.</param>
public sealed record ThemeImageDecision(ThemeImageRole Role, bool Removable, bool Strip = false);

/// <summary>Katalogda saklanan görsel kararı (rapor için).</summary>
public sealed record ThemeImageRecord(int Index, ThemeImageRole Role, int Line, string? SlotId, bool Removable = false,
    bool Stripped = false);

/// <summary>Temanın yuva kataloğu — <c>Template.SlotCatalog</c> (jsonb) olarak saklanır. Etiketler burada değil,
/// projeksiyon anında koddan eklenir.</summary>
public sealed record ThemeSlotCatalog(
    int V,
    int AnalyzerVersion,
    ThemeSlotStatus Status,
    DateTime AnalyzedAt,
    IReadOnlyList<ThemeAnchorResult> Anchors,
    IReadOnlyList<ThemeSlot> Slots,
    IReadOnlyList<string> OccupiedHolders,
    OverridesSummary? Overrides,
    IReadOnlyList<ThemeSlotWarning> Warnings,
    IReadOnlyList<ThemeImageRecord>? Images = null)
{
    public const int CurrentVersion = 1;
    public const int CurrentAnalyzerVersion = 1;
}

/// <summary>Eski tutucu id'leri — v1 betiğinin ve runtime yedeğinin hedefleri (sıra önemli).</summary>
public static class LegacyHolderIds
{
    public static readonly IReadOnlyList<string> Logo = ["company_logo", "companyLogo"];
    public static readonly IReadOnlyList<string> Signature = ["imza", "companyKase"];

    public static IReadOnlyList<string> For(ThemeAssetKind kind) => kind == ThemeAssetKind.Logo ? Logo : Signature;

    public static ThemeAssetKind? KindOf(string id) =>
        Logo.Contains(id) ? ThemeAssetKind.Logo : Signature.Contains(id) ? ThemeAssetKind.Signature : null;
}
