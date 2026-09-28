using System.Security.Cryptography;
using System.Text;

namespace XsltCraft.Application.Auth;

/// <summary>
/// Refresh token üretimi ve özetlenmesi. DB'de yalnız SHA-256 özeti tutulur; veritabanı sızsa bile
/// ham token (çerezdeki değer) elde edilemez. Token 256-bit rastgele olduğu için tuz gerekmez.
/// </summary>
public static class RefreshTokenHasher
{
    public static string Generate() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
