using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots.Catalog;

/// <summary>Yuva ve çapa etiketleri — DB'ye yazılmaz, projeksiyon anında eklenir (değişiklik migration gerektirmez).</summary>
internal static class ThemeSlotLabels
{
    public const string TopGroup = "Üst bölüm";
    public const string MiddleGroup = "Orta bölüm";
    public const string BottomGroup = "Alt bölüm";
    public const string ExistingGroup = "Temadaki görseller";

    public static string Slot(string id, DocumentType type)
    {
        var despatch = type == DocumentType.Despatch;
        var baseId = StripSuffix(id, out var suffix);
        var label = baseId switch
        {
            "existing-logo" => "Temadaki logonun yeri",
            "existing-signature" => "Temadaki imza / kaşenin yeri",
            "seller-top" => despatch ? "Gönderici bilgilerinin üstü" : "Satıcı bilgilerinin üstü",
            "gib-logo-below" => "GİB logosunun altı",
            "qr-below" => "Karekodun altı",
            "customer-side" => "Alıcı bilgilerinin yanı",
            "docinfo-top" => despatch ? "İrsaliye bilgilerinin üstü" : "Fatura bilgilerinin üstü",
            "totals-left" => "Toplamların solu",
            "notes-bottom" => "Notların altı",
            "notes-side" => "Notların yanı",
            "header-left" => "Başlığın solu",
            "header-center" => "Başlığın ortası",
            "header-right" => "Başlığın sağı",
            "legacy-logo" => "Firma logosu alanı",
            "legacy-signature" => "İmza / kaşe alanı",
            _ => baseId,
        };
        return suffix is null ? label : $"{label} ({suffix})";
    }

    public static string Group(string id) => StripSuffix(id, out _) switch
    {
        "existing-logo" or "existing-signature" => ExistingGroup,
        "seller-top" or "gib-logo-below" or "qr-below" or "header-left" or "header-center" or "header-right" => TopGroup,
        "customer-side" or "docinfo-top" => MiddleGroup,
        _ => BottomGroup,
    };

    public static string Anchor(string id, DocumentType type) => id switch
    {
        "qr" => "Karekod",
        "seller" => type == DocumentType.Despatch ? "Gönderici bilgileri" : "Satıcı bilgileri",
        "gibLogo" => "GİB logosu ve belge başlığı",
        "customer" => "Alıcı bilgileri",
        "docInfo" => type == DocumentType.Despatch ? "İrsaliye bilgileri" : "Fatura bilgileri",
        "ettn" => "ETTN",
        "lines" => "Kalemler",
        "totals" => "Toplamlar",
        "notes" => "Notlar",
        _ => id,
    };

    // "legacy-logo-2" → ("legacy-logo", "2")
    private static string StripSuffix(string id, out string? suffix)
    {
        suffix = null;
        var dash = id.LastIndexOf('-');
        if (dash > 0 && int.TryParse(id[(dash + 1)..], out _))
        {
            suffix = id[(dash + 1)..];
            return id[..dash];
        }
        return id;
    }
}
