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
}
