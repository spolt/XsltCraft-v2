using XsltCraft.Infrastructure.Xslt.Worker;

namespace XsltCraft.XsltWorker.Tests.Pool;

public class XsltWorkerOptionsTests
{
    [Fact]
    public void Defaults_AreValid() => Assert.Empty(new XsltWorkerOptions().Validate());

    [Fact]
    public void MemoryKillDisabled_AllowsAnyRecycleThreshold() =>
        Assert.Empty(new XsltWorkerOptions { MaxWorkingSetMb = 0, RecycleWorkingSetMb = 4096 }.Validate());

    [Theory]
    [InlineData(nameof(XsltWorkerOptions.ShutdownGraceMs), 0)]
    [InlineData(nameof(XsltWorkerOptions.ShutdownGraceMs), -1)]
    [InlineData(nameof(XsltWorkerOptions.ShutdownGraceMs), XsltWorkerOptions.MaxShutdownGraceMs + 1)]
    [InlineData(nameof(XsltWorkerOptions.StartupTimeoutMs), 0)]
    [InlineData(nameof(XsltWorkerOptions.StartupTimeoutMs), XsltWorkerOptions.MaxStartupTimeoutMs + 1)]
    [InlineData(nameof(XsltWorkerOptions.RenderTimeoutMs), 0)]
    [InlineData(nameof(XsltWorkerOptions.ValidateTimeoutMs), XsltWorkerOptions.MaxRequestTimeoutMs + 1)]
    [InlineData(nameof(XsltWorkerOptions.PoolSize), 0)]
    [InlineData(nameof(XsltWorkerOptions.MaxRequestsPerWorker), 0)]
    [InlineData(nameof(XsltWorkerOptions.MaxWorkerAgeMinutes), 0)]
    [InlineData(nameof(XsltWorkerOptions.WorkingSetPollMs), 5)]
    [InlineData(nameof(XsltWorkerOptions.RespawnBackoffInitialMs), 0)]
    [InlineData(nameof(XsltWorkerOptions.BreakerFailureThreshold), 0)]
    [InlineData(nameof(XsltWorkerOptions.BreakerWindowSeconds), 0)]
    [InlineData(nameof(XsltWorkerOptions.BreakerOpenSeconds), 0)]
    [InlineData(nameof(XsltWorkerOptions.MaxQueue), -1)]
    [InlineData(nameof(XsltWorkerOptions.QueueWaitMs), -1)]
    [InlineData(nameof(XsltWorkerOptions.AffinityHashesPerWorker), -1)]
    [InlineData(nameof(XsltWorkerOptions.GcHeapHardLimitMb), -1)]
    public void InvalidValue_Rejected(string property, int value)
    {
        var options = new XsltWorkerOptions();
        typeof(XsltWorkerOptions).GetProperty(property)!.SetValue(options, value);

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public void RecycleAboveKillThreshold_Rejected()
    {
        var errors = new XsltWorkerOptions { MaxWorkingSetMb = 512, RecycleWorkingSetMb = 768 }.Validate();

        Assert.Contains(errors, e => e.Contains(nameof(XsltWorkerOptions.RecycleWorkingSetMb), StringComparison.Ordinal));
    }

    [Fact]
    public void BackoffMaxBelowInitial_Rejected() =>
        Assert.NotEmpty(new XsltWorkerOptions { RespawnBackoffInitialMs = 1000, RespawnBackoffMaxMs = 500 }.Validate());
}
