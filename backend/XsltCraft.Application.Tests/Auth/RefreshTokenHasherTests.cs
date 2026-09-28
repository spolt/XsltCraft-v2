using XsltCraft.Application.Auth;

namespace XsltCraft.Application.Tests.Auth;

public class RefreshTokenHasherTests
{
    [Fact]
    public void Generate_ProducesUniqueUrlSafeTokens()
    {
        var a = RefreshTokenHasher.Generate();
        var b = RefreshTokenHasher.Generate();

        Assert.NotEqual(a, b);
        Assert.Equal(43, a.Length); // 32 bayt → padding'siz base64url
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }

    [Fact]
    public void Hash_IsDeterministicAndDoesNotContainRawToken()
    {
        var raw = RefreshTokenHasher.Generate();
        var hash = RefreshTokenHasher.Hash(raw);

        Assert.Equal(hash, RefreshTokenHasher.Hash(raw));
        Assert.Equal(64, hash.Length); // DB kolonu varchar(64)
        Assert.DoesNotContain(raw, hash);
        Assert.NotEqual(hash, RefreshTokenHasher.Hash(raw + "x"));
    }
}
