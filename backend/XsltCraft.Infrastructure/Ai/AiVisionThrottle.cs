using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Application.Ai.Vision;

namespace XsltCraft.Infrastructure.Ai;

/// <summary>
/// Kullanıcı başına görselli istek sınırı (FixedWindow, dakikada N). Singleton; boşta kalan
/// kullanıcı bölümleri limiter tarafından kendiliğinden temizlenir.
/// </summary>
public sealed class AiVisionThrottle : IAiVisionThrottle, IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _limiter;

    public AiVisionThrottle(IOptions<AiOptions> options)
    {
        var permits = Math.Max(1, options.Value.Vision.PerUserPerMinute);
        _limiter = PartitionedRateLimiter.Create<Guid, Guid>(userId =>
            RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public bool TryAcquire(Guid userId)
    {
        using var lease = _limiter.AttemptAcquire(userId);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
