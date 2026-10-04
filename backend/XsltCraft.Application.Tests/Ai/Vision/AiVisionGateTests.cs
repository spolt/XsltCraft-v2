using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.Tests.Imaging;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.Ai.Vision;

public class AiVisionGateTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly VisionOptions Options = new();

    private static AiImagePayload Png() => new("image/png", Convert.ToBase64String(ImageFixtures.Png(800, 600)));

    private sealed class FakeAvailability(params string[] providers) : IAiVisionAvailability
    {
        public Task<IReadOnlyList<string>> ResolveProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(providers);
    }

    private sealed class FakeThrottle(bool allow) : IAiVisionThrottle
    {
        public int Calls { get; private set; }
        public bool TryAcquire(Guid userId) { Calls++; return allow; }
    }

    private sealed class FakeEntitlements(UserRole role, MembershipPlan plan) : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(EntitlementPolicy.Compute(role, plan, null, new MembershipOptions(), DateTime.UtcNow));
    }

    private static (AiVisionGate Gate, FakeThrottle Throttle) Create(
        MembershipPlan plan = MembershipPlan.Pro,
        UserRole role = UserRole.User,
        bool throttleAllows = true,
        params string[] providers)
    {
        var throttle = new FakeThrottle(throttleAllows);
        var gate = new AiVisionGate(
            new FakeAvailability(providers.Length == 0 ? ["gemini"] : providers),
            new FakeEntitlements(role, plan),
            throttle,
            new AiImageValidator(Options),
            Options);
        return (gate, throttle);
    }

    [Fact]
    public async Task ValidImage_ForPro_IsAllowed()
    {
        var (gate, _) = Create();

        var result = await gate.EvaluateAsync(UserId, [Png(), Png(), Png()]);

        Assert.True(result.Allowed);
        Assert.Equal(3, result.Images.Count);
    }

    [Fact]
    public async Task NoProvider_IsUnavailable_AndThrottleNotConsumed()
    {
        var throttle = new FakeThrottle(true);
        var gate = new AiVisionGate(new FakeAvailability(), new FakeEntitlements(UserRole.User, MembershipPlan.Pro),
            throttle, new AiImageValidator(Options), Options);

        var result = await gate.EvaluateAsync(UserId, [Png()]);

        Assert.Equal(VisionDenyReason.Unavailable, result.Reason);
        Assert.Equal(AiVisionGate.CodeUnavailable, result.ErrorCode);
        Assert.Equal(0, throttle.Calls);
    }

    [Fact]
    public async Task Free_WithTwoImages_IsDeniedWithUpgrade_AndThrottleNotConsumed()
    {
        var (gate, throttle) = Create(MembershipPlan.Free);

        var result = await gate.EvaluateAsync(UserId, [Png(), Png()]);

        Assert.Equal(VisionDenyReason.CountExceeded, result.Reason);
        Assert.True(result.Upgrade);
        Assert.Equal(0, throttle.Calls);
    }

    [Fact]
    public async Task Free_WithOneImage_IsAllowed()
    {
        var (gate, _) = Create(MembershipPlan.Free);
        Assert.True((await gate.EvaluateAsync(UserId, [Png()])).Allowed);
    }

    [Fact]
    public async Task Pro_OverTechnicalCap_IsDeniedWithoutUpgrade()
    {
        var (gate, _) = Create(MembershipPlan.Pro);

        var result = await gate.EvaluateAsync(UserId, [Png(), Png(), Png(), Png()]);

        Assert.Equal(VisionDenyReason.CountExceeded, result.Reason);
        Assert.False(result.Upgrade);
    }

    [Theory]
    [InlineData(UserRole.Editor)]
    [InlineData(UserRole.Admin)]
    public async Task Privileged_IsCappedByTechnicalLimit(UserRole role)
    {
        var (gate, _) = Create(MembershipPlan.Free, role);

        Assert.True((await gate.EvaluateAsync(UserId, [Png(), Png(), Png()])).Allowed);
        Assert.Equal(VisionDenyReason.CountExceeded, (await gate.EvaluateAsync(UserId, [Png(), Png(), Png(), Png()])).Reason);
    }

    [Fact]
    public async Task Throttled_IsRateLimited()
    {
        var (gate, _) = Create(throttleAllows: false);

        var result = await gate.EvaluateAsync(UserId, [Png()]);

        Assert.Equal(VisionDenyReason.RateLimited, result.Reason);
        Assert.Equal(AiVisionGate.CodeRateLimited, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidImage_IsDeniedWithValidatorCode()
    {
        var (gate, _) = Create();

        var result = await gate.EvaluateAsync(UserId, [new AiImagePayload("image/png", Convert.ToBase64String(ImageFixtures.Gif()))]);

        Assert.Equal(VisionDenyReason.Invalid, result.Reason);
        Assert.Equal(AiImageValidator.CodeType, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(1, 3, 1)]
    [InlineData(5, 3, 3)]
    public void EffectiveLimit_PlanNeverExceedsTechnicalCap(int plan, int cap, int expected)
        => Assert.Equal(expected, AiVisionGate.EffectiveLimit(plan, cap));
}
