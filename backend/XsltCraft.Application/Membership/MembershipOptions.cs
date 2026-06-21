namespace XsltCraft.Application.Membership;

/// <summary>
/// Üyelik planlarının (Free / Pro) yetenek ve günlük kota politikası. appsettings "Membership" bölümünden
/// override edilebilir; aksi halde buradaki güvenli varsayımlar geçerlidir. Editör/Admin rolleri bu
/// politikayı tümden bypass eder (bkz. <see cref="EntitlementPolicy"/>).
/// </summary>
public class MembershipOptions
{
    public const string SectionName = "Membership";

    /// <summary>Standart (ücretsiz) kullanıcı: indiremez, ham XSLT saklayamaz/erişemez, ücretli tema kullanamaz, günde 1 AI sorusu.</summary>
    public PlanLimits Free { get; set; } = new()
    {
        CanDownloadGridXslt = false,
        CanSaveRawXslt = false,
        CanAccessRawXslt = false,
        CanUsePremiumThemes = false,
        DailyAiRequestLimit = 1,
        DailyAiTokenLimit = 0,
        DailyTemplateExportLimit = 0,
    };

    /// <summary>Pro kullanıcı: tam özellik ama günde 3 indirme + 50.000 token ile sınırlı (toplu yeniden-satış engeli).</summary>
    public PlanLimits Pro { get; set; } = new()
    {
        CanDownloadGridXslt = true,
        CanSaveRawXslt = true,
        CanAccessRawXslt = true,
        CanUsePremiumThemes = true,
        DailyAiRequestLimit = 0,        // 0 = sınırsız istek (token bütçesiyle yönetilir)
        DailyAiTokenLimit = 50_000,
        DailyTemplateExportLimit = 3,
    };
}

/// <summary>Tek bir planın yetenekleri ve günlük limitleri. Limitlerde <c>0 = sınırsız</c>.</summary>
public class PlanLimits
{
    /// <summary>Grid canvas'ta sıfırdan tasarlanan şablonun production XSLT'sini indirebilir mi.</summary>
    public bool CanDownloadGridXslt { get; set; }
    /// <summary>XSLT Editör/Şablonlarım'a ham XSLT kaydedebilir/saklayabilir mi.</summary>
    public bool CanSaveRawXslt { get; set; }
    /// <summary>Kaydedilmiş grid taslağının ham XSLT içeriğine erişebilir mi.</summary>
    public bool CanAccessRawXslt { get; set; }
    /// <summary>Tema kütüphanesindeki ücretli temaları "kullan"abilir mi.</summary>
    public bool CanUsePremiumThemes { get; set; }
    /// <summary>Günlük AI istek (soru) limiti. 0 = sınırsız.</summary>
    public int DailyAiRequestLimit { get; set; }
    /// <summary>Günlük yaklaşık AI token limiti. 0 = sınırsız.</summary>
    public int DailyAiTokenLimit { get; set; }
    /// <summary>Günlük production XSLT indirme limiti. 0 = sınırsız. (CanDownloadGridXslt=false ise hiç indiremez.)</summary>
    public int DailyTemplateExportLimit { get; set; }
}
