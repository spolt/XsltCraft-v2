namespace XsltCraft.Application.Ai;

/// <summary>
/// Sağlayıcı sıralama kararı (saf, test edilebilir).
/// "auto" modda büyük XSLT'li assistant istekleri Gemini'ye öncelikli yönlenir:
/// Gemini tam dosyayı gördüğü için "X alanını değiştir" tarzı sorulara doğru cevap verir;
/// Ollama'nın küçük penceresi büyük şablonlarda yalnızca özet görür. Ollama yedek kalır.
/// </summary>
public static class ProviderRouting
{
    /// <returns>"gemini" → Gemini öncelikli; diğer her değer → Ollama öncelikli.</returns>
    public static string Resolve(string preferred, AiTaskKind task, int xsltLength, int largeXsltThresholdChars)
    {
        // Açık tercih (admin flag / appsettings) her zaman kazanır.
        if (preferred is "gemini" or "ollama") return preferred;

        if (task == AiTaskKind.Assistant
            && largeXsltThresholdChars > 0
            && xsltLength > largeXsltThresholdChars)
            return "gemini";

        return "ollama";
    }

    /// <summary>
    /// Görselli istek için sağlayıcı sırası (yalnız görseli işleyebilenler). Boş liste = vision kullanılamaz.
    /// "ollama" (katı tercih): yerel vision modeli önce; Gemini yalnız <paramref name="geminiFallback"/> açıksa yedek.
    /// "auto"/"gemini"/bilinmeyen: Gemini önce (görseli koda eşlemede daha güçlü), yerel vision modeli yedek.
    /// </summary>
    public static IReadOnlyList<string> ResolveVision(
        string preferred, bool ollamaVision, bool geminiVision, bool geminiFallback)
    {
        var order = preferred == "ollama"
            ? (geminiFallback ? new[] { "ollama", "gemini" } : ["ollama"])
            : ["gemini", "ollama"];

        return order
            .Where(name => name == "ollama" ? ollamaVision : geminiVision)
            .ToList();
    }
}
