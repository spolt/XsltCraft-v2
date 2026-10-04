using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Infrastructure.Ai;

namespace XsltCraft.Infrastructure.Tests.Ai;

public class AiVisionThrottleTests
{
    [Fact]
    public void PerUserLimit_IsEnforced_AndUsersAreIsolated()
    {
        using var sut = new AiVisionThrottle(Options.Create(new AiOptions { Vision = new VisionOptions { PerUserPerMinute = 2 } }));
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        Assert.True(sut.TryAcquire(alice));
        Assert.True(sut.TryAcquire(alice));
        Assert.False(sut.TryAcquire(alice));
        Assert.True(sut.TryAcquire(bob));
    }
}
