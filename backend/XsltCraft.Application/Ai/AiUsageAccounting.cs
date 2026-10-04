namespace XsltCraft.Application.Ai;

/// <summary>
/// Bir AI isteğinin kotaya etkisi (saf, test edilebilir). Kural: gate'i geçen her istek sayılır;
/// tek istisna görselli istekte HİÇBİR sağlayıcının çıktı üretmemesi (ör. yerel vision modeli yok).
/// İstemci iptali istisna DEĞİLDİR — aksi halde ilk token'dan önce bağlantıyı kesen kullanıcı,
/// sağlayıcı görsel girdisini faturalamış olsa bile sınırsız istek atabilirdi.
/// </summary>
public static class AiUsageAccounting
{
    /// <returns>(Sayaç artsın mı, eklenecek yaklaşık token).</returns>
    public static (bool CountRequest, int Tokens) Compute(
        int imageCount, int outputChars, bool clientCancelled, int tokenCostPerImage)
    {
        var count = imageCount == 0 || outputChars > 0 || clientCancelled;
        if (!count) return (false, 0);

        var tokens = outputChars > 0 ? outputChars / 4 + 1 : 0;
        if (imageCount > 0) tokens += imageCount * tokenCostPerImage;
        return (true, tokens);
    }
}

/// <summary>Geri bildirim kaydındaki işaretler (istemci soruya ekler).</summary>
public static class AiFeedbackMarkers
{
    /// <summary>
    /// Soruya ekran görüntüsü eklendiğini belirtir. Bu cevaplar görseldeki (başka kullanıcıya ait olabilecek)
    /// verileri yansıtabileceği için global örnek havuzuna terfi ettirilemez.
    /// </summary>
    public const string ImageAttached = "[ekran görüntüsü ekli]";
}
