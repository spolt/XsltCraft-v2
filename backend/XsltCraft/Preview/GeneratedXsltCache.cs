namespace XsltCraft.Api.Preview;

/// <summary>
/// Kullanıcı şablonundan üretilen XSLT'nin storage önbellek anahtarı — tek kaynak. Görseller her zaman base64
/// gömülü üretilir; eski sürüm anahtarlı önbellekler (<c>templates/{id}.xslt</c>, asset URL'li olabilir) güncel
/// sürüme uymadığı için okunmaz, yeniden üretilir.
/// </summary>
public static class GeneratedXsltCache
{
    public const string Version = "b64";

    public static string PathFor(Guid templateId) => $"templates/{templateId}.{Version}.xslt";

    /// <summary>Şablonun kendi üretilmiş önbelleği mi (her sürüm)? Klonun referansladığı tema XSLT'si değil.</summary>
    public static bool IsOwn(string? path, Guid templateId) =>
        path is not null && path.StartsWith($"templates/{templateId}.", StringComparison.Ordinal);

    /// <summary>Şablonun kendi önbelleği ama güncel sürümde değil → okunmamalı.</summary>
    public static bool IsStale(string? path, Guid templateId) => IsOwn(path, templateId) && path != PathFor(templateId);
}
