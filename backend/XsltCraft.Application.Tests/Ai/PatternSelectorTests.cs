using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Tests.Ai;

public class PatternSelectorTests
{
    private static string[] Ids(string request, string? selection = null, string? xslt = null)
    {
        var req = new AiRequest { UserRequest = request, Selection = selection, UserXslt = xslt };
        return PatternSelector.Select(req).Select(p => p.Id).OrderBy(x => x).ToArray();
    }

    public static TheoryData<string, string?, string?, string[]> Cases => new()
    {
        // Plan'dan alınan temel test case'leri
        { "note prefix gizle",              null, null, ["invoice-note"] },
        { "satıcı adresi sadece il ilçe",   null, null, ["supplier-party-address"] },
        { "yeni kolon ekle",                null, null, ["invoice-line"] },
        { "",                               null, null, [] },   // boş istek → fallback yok (0 kelime)
        { "selam",                          null, null, [] },   // selamlama → fallback yok (1 kelime)

        // Ek case'ler
        { "alıcı adresini gizle",           null, null, ["customer-party-address"] },
        { "ödenecek tutarı göster",         null, null, ["legal-monetary-total"] },
        { "satıcı kişi ismi düzenle",       null, null, ["supplier-party-person"] },
        { "fatura tarihi formatı",          null, null, ["invoice-header"] },
        { "alıcı kişi bilgisi",             null, null, ["customer-party-person"] },
        { "kdv tutarı hesaplama",           null, null, ["legal-monetary-total"] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Select_ReturnsExpectedPatternIds(
        string request, string? selection, string? xslt, string[] expectedIds)
    {
        var result = Ids(request, selection, xslt);
        Assert.Equal(expectedIds.OrderBy(x => x).ToArray(), result);
    }

    [Fact]
    public void Select_XsltSignal_InvoiceNote()
    {
        // XSLT içeriğinde cbc:Note varsa invoice-note skora eklenir
        var req = new AiRequest
        {
            UserRequest = "bu bloğu düzenle",
            UserXslt = "<xsl:for-each select=\"//n1:Invoice/cbc:Note\"/>",
        };
        var ids = PatternSelector.Select(req).Select(p => p.Id).ToArray();
        Assert.Contains("invoice-note", ids);
    }

    [Fact]
    public void Select_MaxFourPatterns()
    {
        // Tüm trigger'ları tetikleyen yapay bir request
        var req = new AiRequest
        {
            UserRequest = "note prefix satır kolon ödenecek tutar fatura tarihi satıcı adres alıcı adres",
        };
        var result = PatternSelector.Select(req);
        Assert.True(result.Count <= 4, $"En fazla 4 pattern seçilmeli, {result.Count} seçildi.");
    }
}
