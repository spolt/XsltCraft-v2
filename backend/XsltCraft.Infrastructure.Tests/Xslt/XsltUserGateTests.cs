using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt;

namespace XsltCraft.Infrastructure.Tests.Xslt;

public sealed class XsltUserGateTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ManualTimeProvider _time = new();

    private XsltUserGate Create(XsltUserGateOptions? options = null) =>
        new(Options.Create(new XsltExecutionOptions { UserGate = options ?? new XsltUserGateOptions() }), _time, NullLogger<XsltUserGate>.Instance);

    private static IXsltGateLease Acquire(XsltUserGate gate, Guid user, XsltGateLane lane, XsltExecutionPlace place = XsltExecutionPlace.Isolated)
    {
        Assert.True(gate.TryAcquire(user, lane, place, out var lease, out var denial), denial?.Message);
        return lease;
    }

    private static XsltGateDenial Deny(XsltUserGate gate, Guid user, XsltGateLane lane, XsltExecutionPlace place = XsltExecutionPlace.Isolated)
    {
        Assert.False(gate.TryAcquire(user, lane, place, out _, out var denial));
        return denial;
    }

    /// <summary>Bir yürütmeyi verilen sonuçla bitirir (bildir + bırak).</summary>
    private static void Run(XsltUserGate gate, Guid user, XsltGateLane lane, XsltFailureKind? outcome, XsltExecutionPlace place = XsltExecutionPlace.Isolated)
    {
        using var lease = Acquire(gate, user, lane, place);
        lease.ReportOutcome(outcome);
    }

    [Fact]
    public void UserGate_SecondConcurrent_UserBusy()
    {
        var gate = Create();
        var first = Acquire(gate, Alice, XsltGateLane.Raw);

        var denial = Deny(gate, Alice, XsltGateLane.Raw);

        Assert.Equal(XsltUserGate.BusyCode, denial.ErrorCode);
        Assert.Null(denial.RetryAfter);

        // Diğer şeritler bağımsız (editör önizleme + doğrulamayı paralel çağırır).
        using (Acquire(gate, Alice, XsltGateLane.Validate))
        using (Acquire(gate, Alice, XsltGateLane.UserTemplate))
        using (Acquire(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.InProcess))
        {
        }

        first.Dispose();
        first.Dispose(); // idempotent: sayaç eksiye düşmez
        using (Acquire(gate, Alice, XsltGateLane.Raw))
            Deny(gate, Alice, XsltGateLane.Raw);
    }

    [Fact]
    public void ConfiguredConcurrency_IsHonored()
    {
        var gate = Create(new XsltUserGateOptions { UserTemplateMaxConcurrent = 2 });

        using var a = Acquire(gate, Alice, XsltGateLane.UserTemplate);
        using var b = Acquire(gate, Alice, XsltGateLane.UserTemplate);
        Deny(gate, Alice, XsltGateLane.UserTemplate);
    }

    [Theory]
    [InlineData(XsltFailureKind.Timeout)]
    [InlineData(XsltFailureKind.EngineCrashed)]
    [InlineData(XsltFailureKind.MemoryLimit)]
    public void Abuse_ThreeKills_Blocks10Min(XsltFailureKind kill)
    {
        var gate = Create();

        Run(gate, Alice, XsltGateLane.Raw, kill);
        Run(gate, Alice, XsltGateLane.UserTemplate, kill);
        _time.Advance(TimeSpan.FromMinutes(2));
        Run(gate, Alice, XsltGateLane.Validate, kill);

        // Tüm izole şeritler kapalı; süreç içi grid etkilenmez.
        var denial = Deny(gate, Alice, XsltGateLane.Raw);
        Assert.Equal(XsltUserGate.BlockedCode, denial.ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(10), denial.RetryAfter);
        Deny(gate, Alice, XsltGateLane.Validate);
        Deny(gate, Alice, XsltGateLane.UserTemplate);
        Deny(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.Isolated); // script'li grid de worker'da
        using (Acquire(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.InProcess))
        {
        }

        _time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), Deny(gate, Alice, XsltGateLane.Raw).RetryAfter);

        _time.Advance(TimeSpan.FromSeconds(1));
        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }
    }

    [Theory]
    [InlineData(XsltFailureKind.Compile)]
    [InlineData(XsltFailureKind.Runtime)]
    [InlineData(XsltFailureKind.Security)]
    [InlineData(XsltFailureKind.OutputLimit)]
    [InlineData(XsltFailureKind.Busy)]
    [InlineData(XsltFailureKind.EngineUnavailable)]
    public void NonKillFailures_DoNotCount(XsltFailureKind failure)
    {
        var gate = Create();

        for (var i = 0; i < 5; i++)
            Run(gate, Alice, XsltGateLane.Raw, failure);
        Run(gate, Alice, XsltGateLane.Raw, null);

        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }
    }

    [Fact]
    public void GridAbuse_TwoTimeouts_Blocks()
    {
        var gate = Create();

        Run(gate, Alice, XsltGateLane.Grid, XsltFailureKind.Timeout, XsltExecutionPlace.InProcess);
        // Süreç içinde yalnız Timeout sayılır.
        Run(gate, Alice, XsltGateLane.Grid, XsltFailureKind.MemoryLimit, XsltExecutionPlace.InProcess);
        using (Acquire(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.InProcess))
        {
        }

        Run(gate, Alice, XsltGateLane.Grid, XsltFailureKind.Timeout, XsltExecutionPlace.InProcess);

        var denial = Deny(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.InProcess);
        Assert.Equal(XsltUserGate.BlockedCode, denial.ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(10), denial.RetryAfter);

        // Worker rotaları bağımsız.
        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }

        _time.Advance(TimeSpan.FromMinutes(10));
        using (Acquire(gate, Alice, XsltGateLane.Grid, XsltExecutionPlace.InProcess))
        {
        }
    }

    [Fact]
    public void Abuse_WindowExpires()
    {
        var gate = Create();

        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.Timeout);
        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.EngineCrashed);
        _time.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.MemoryLimit);

        // Pencerede yalnız 1 ihlal → blok yok.
        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }

        _time.Advance(TimeSpan.FromMinutes(1));
        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.Timeout);
        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.Timeout);
        Deny(gate, Alice, XsltGateLane.Raw);
    }

    [Fact]
    public void DifferentUsers_Independent()
    {
        var gate = Create();
        using var alice = Acquire(gate, Alice, XsltGateLane.Raw);
        for (var i = 0; i < 3; i++)
            Run(gate, Bob, XsltGateLane.Validate, XsltFailureKind.Timeout);

        Deny(gate, Bob, XsltGateLane.Raw);
        using (Acquire(gate, Bob, XsltGateLane.Grid, XsltExecutionPlace.InProcess))
        {
        }

        Deny(gate, Alice, XsltGateLane.Raw); // Alice'in kendi izni
        using (Acquire(gate, Alice, XsltGateLane.Validate))
        {
        }
    }

    [Fact]
    public void ReportAfterDispose_IsIgnored_AndReportedOnce()
    {
        var gate = Create(new XsltUserGateOptions { IsolatedAbuseThreshold = 2 });

        var lease = Acquire(gate, Alice, XsltGateLane.Raw);
        lease.ReportOutcome(XsltFailureKind.Timeout);
        lease.ReportOutcome(XsltFailureKind.Timeout); // ikinci bildirim sayılmaz
        lease.Dispose();

        var late = Acquire(gate, Alice, XsltGateLane.Raw);
        late.Dispose();
        late.ReportOutcome(XsltFailureKind.Timeout); // bırakıldıktan sonra → yok sayılır

        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }
    }

    [Fact]
    public void IdleUsers_AreSwept_BoundedMemory()
    {
        var gate = Create();
        for (var i = 0; i < 1000; i++)
            Run(gate, Guid.NewGuid(), XsltGateLane.Raw, null);
        Run(gate, Alice, XsltGateLane.Raw, XsltFailureKind.Timeout); // pencerede ihlal → tutulur
        using var busy = Acquire(gate, Bob, XsltGateLane.Raw);       // izin tutuyor → tutulur
        Assert.Equal(1002, gate.TrackedUsers);

        _time.Advance(TimeSpan.FromMinutes(1));
        Run(gate, Guid.NewGuid(), XsltGateLane.Raw, null); // süpürmeyi tetikler

        Assert.Equal(3, gate.TrackedUsers); // Alice, Bob + yeni kullanıcı

        _time.Advance(TimeSpan.FromMinutes(10));
        Run(gate, Guid.NewGuid(), XsltGateLane.Raw, null);
        Assert.Equal(2, gate.TrackedUsers); // Bob (hâlâ meşgul) + yeni kullanıcı
    }

    [Fact]
    public async Task Concurrent_Acquire_NeverExceedsLimit()
    {
        var gate = Create();
        var inside = 0;
        var maxInside = 0;

        await Task.WhenAll(Enumerable.Range(0, 64).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                if (!gate.TryAcquire(Alice, XsltGateLane.Raw, XsltExecutionPlace.Isolated, out var lease, out _))
                    continue;
                using (lease)
                {
                    var now = Interlocked.Increment(ref inside);
                    InterlockedMax(ref maxInside, now);
                    Interlocked.Decrement(ref inside);
                }
            }
        })));

        Assert.Equal(1, maxInside);
        using (Acquire(gate, Alice, XsltGateLane.Raw))
        {
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    /// <summary>Elle ilerletilen saat (yalnız <see cref="TimeProvider.GetUtcNow"/> kullanılır).</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
