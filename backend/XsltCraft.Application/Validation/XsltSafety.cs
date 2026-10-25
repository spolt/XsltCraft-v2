using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Application.Validation;

/// <summary>
/// Kullanıcı/admin kaynaklı XSLT için tamamen yasakçı (fail-closed, izin listesi) güvenlik
/// cephesi. Asıl mantık <see cref="XsltSafetyScanner"/>'dadır; bu sınıf geriye dönük uyumlu bir
/// cephe (façade) sağlar.
///
/// <para><b>Neden:</b> kullanıcı binding XPath'i üretilen stylesheet'e doğrudan gömülür, admin ham
/// tema (.xslt) yükleyebilir ve bu içerik render/validate/analiz hatlarına gider. Dış kaynak
/// çağıran fonksiyonlar (<c>document()</c>/<c>doc()</c>/<c>unparsed-text()</c> …) SSRF/dosya okuma,
/// uzantılar ise sandbox kaçışı açar. Bu yüzden içerik motora ulaşmadan **fail-closed** taranır.</para>
///
/// <para><b>Çağıranlar (mod eşlemesi):</b></para>
/// <list type="bullet">
///   <item><b>Execute</b> (Security + Unsupported): <c>XsltGeneratorService.Validate</c>
///   (generator çıktısı render edilecek).</item>
///   <item><b>Store</b> (yalnız Security): <c>UserXsltTemplateController</c> (toplu ham yükleme),
///   <c>AdminController</c> (ham tema), <c>ThemeUploadProcessor</c>, <c>ThemeSlotAnalyzer</c>,
///   <c>ThemeOverridesProcessor</c>, <c>ThemeAssetComposer</c> ve <c>FixedNoteInjector</c>.</item>
/// </list>
/// </summary>
public static class XsltSafety
{
    private static readonly XsltSafetyScanner Scanner = new();

    /// <summary>
    /// İçeriği belirtilen modda tarar. <see cref="XsltSafetyMode.Execute"/> Security+Unsupported,
    /// <see cref="XsltSafetyMode.Store"/> yalnız Security bulgularını engel olarak döndürür.
    /// </summary>
    public static XsltSafetyReport Scan(string? xslt, XsltSafetyMode mode) => Scanner.Scan(xslt, mode);

    /// <summary>
    /// Geriye dönük uyumlu cephe: <see cref="XsltSafetyMode.Store"/> taraması yapar; tehdit bulursa
    /// açıklayıcı Türkçe mesaj, güvenliyse <c>null</c> döndürür.
    /// </summary>
    public static string? FindThreat(string? xslt) => Scanner.Scan(xslt, XsltSafetyMode.Store).FirstMessage;
}
