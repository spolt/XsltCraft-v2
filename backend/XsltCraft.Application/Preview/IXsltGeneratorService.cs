namespace XsltCraft.Application.Preview;

public interface IXsltGeneratorService
{
    /// <summary>
    /// Block tree JSON'undan geçerli bir XSLT stylesheet üretir.
    /// </summary>
    /// <returns>
    /// Başarıda (Xslt: string, Error: null),
    /// hata durumunda (Xslt: null, Error: mesaj).
    /// </returns>
    (string? Xslt, string? Error) Generate(BlockTreeDto tree, Dictionary<string, string>? assetBase64 = null);

    /// <summary>
    /// JSON string'den BlockTreeDto deserialize eder, ardından Generate çağırır.
    /// </summary>
    (string? Xslt, string? Error) GenerateFromJson(string blockTreeJson);

    /// <summary>
    /// V2 (grid-based) block tree'den mutlak konumlu XSLT üretir.
    /// </summary>
    (string? Xslt, string? Error) GenerateV2(BlockTreeV2Dto tree, Dictionary<string, string>? assetBase64 = null);

    /// <summary>
    /// Son <c>Generate*()</c> çağrısında taşınamaz XPath yüzünden yer tutucuyla değiştirilen bloklar (G3 — blok
    /// düzeyinde yumuşak düşüş). Boş → tüm bloklar üretildi.
    /// </summary>
    IReadOnlyList<XsltGenerationWarning> Warnings { get; }
}

/// <summary>Yer tutucuyla değiştirilen blok: <paramref name="Code"/> = <c>XPATH_TOO_LONG</c> / <c>XPATH_TOO_COMPLEX</c> / <c>XPATH_NOT_PORTABLE</c>.</summary>
public sealed record XsltGenerationWarning(string BlockId, string Code, string XPath);
