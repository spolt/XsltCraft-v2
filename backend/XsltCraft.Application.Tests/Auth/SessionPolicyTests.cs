using XsltCraft.Application.Auth;

namespace XsltCraft.Application.Tests.Auth;

public class SessionPolicyTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    private readonly SessionPolicy _policy = new(new SessionOptions
    {
        IdleTimeoutMinutes = 120,
        AbsoluteLifetimeHours = 12,
        RotationGraceSeconds = 30,
    });

    private static RefreshTokenState Active(DateTime expiresAt, DateTime sessionExpiresAt) =>
        new(expiresAt, sessionExpiresAt, RevokedAt: null, WasRotated: false);

    [Fact]
    public void StartSession_UsesIdleWindowAndAbsoluteLimit()
    {
        var (expiresAt, sessionExpiresAt) = _policy.StartSession(T0);

        Assert.Equal(T0.AddHours(2), expiresAt);
        Assert.Equal(T0.AddHours(12), sessionExpiresAt);
    }

    [Fact]
    public void NextExpiry_SlidesIdleWindow()
    {
        var now = T0.AddHours(3);
        Assert.Equal(now.AddHours(2), _policy.NextExpiry(now, T0.AddHours(12)));
    }

    [Fact]
    public void NextExpiry_NeverExceedsAbsoluteLimit()
    {
        var now = T0.AddHours(11);
        Assert.Equal(T0.AddHours(12), _policy.NextExpiry(now, T0.AddHours(12)));
    }

    [Fact]
    public void Evaluate_ActiveToken_Rotates()
    {
        var token = Active(T0.AddHours(2), T0.AddHours(12));
        Assert.Equal(RefreshDecision.Rotate, _policy.Evaluate(token, T0.AddMinutes(90)));
    }

    [Fact]
    public void Evaluate_AfterIdleTimeout_Expires()
    {
        var token = Active(T0.AddHours(2), T0.AddHours(12));
        Assert.Equal(RefreshDecision.Expired, _policy.Evaluate(token, T0.AddHours(2)));
    }

    [Fact]
    public void Evaluate_AfterAbsoluteLifetime_ExpiresEvenIfIdleWindowOpen()
    {
        // Mutlak sınır, bozuk/elle girilmiş bir ExpiresAt'ten bağımsız olarak uygulanır.
        var token = Active(T0.AddHours(13), T0.AddHours(12));
        Assert.Equal(RefreshDecision.Expired, _policy.Evaluate(token, T0.AddHours(12)));
    }

    [Fact]
    public void Evaluate_RotatedTokenWithinGrace_IsConcurrentRotation()
    {
        var token = new RefreshTokenState(T0.AddHours(2), T0.AddHours(12), RevokedAt: T0, WasRotated: true);
        Assert.Equal(RefreshDecision.ConcurrentRotation, _policy.Evaluate(token, T0.AddSeconds(30)));
    }

    [Fact]
    public void Evaluate_RotatedTokenAfterGrace_IsReuse()
    {
        var token = new RefreshTokenState(T0.AddHours(2), T0.AddHours(12), RevokedAt: T0, WasRotated: true);
        Assert.Equal(RefreshDecision.ReuseDetected, _policy.Evaluate(token, T0.AddSeconds(31)));
    }

    [Fact]
    public void Evaluate_LoggedOutToken_IsExpiredNotReuse()
    {
        // Logout / şifre değişikliğiyle iptal edilen token hırsızlık sinyali değildir;
        // diğer cihazlardaki oturumları düşürmemeli.
        var token = new RefreshTokenState(T0.AddHours(2), T0.AddHours(12), RevokedAt: T0, WasRotated: false);
        Assert.Equal(RefreshDecision.Expired, _policy.Evaluate(token, T0.AddMinutes(5)));
    }
}
