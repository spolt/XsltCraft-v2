using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XsltCraft.Tests.Support;

/// <summary>
/// Golden snapshot'larda gürültü yaratan ama deterministik içerikleri kısaltır.
/// Base64 data URI'ler (gömülü GİB logosu, test asset'i) uzunluk + SHA-256 önekiyle
/// değiştirilir: snapshot okunur kalır, içerik değişirse özet değiştiği için yine yakalanır.
/// </summary>
public static partial class SnapshotScrubber
{
    public static string ScrubDataUris(string text) =>
        DataUri().Replace(text, m =>
        {
            var data = m.Groups["data"].Value;
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(data)))[..16];
            return $"data:{m.Groups["mime"].Value};base64,[scrubbed len={data.Length} sha256={hash}]";
        });

    [GeneratedRegex(@"data:(?<mime>[A-Za-z0-9.+\-]+/[A-Za-z0-9.+\-]+);base64,(?<data>[A-Za-z0-9+/=]+)")]
    private static partial Regex DataUri();
}
