using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Ai;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Ai;

public interface IAiFeatureFlagService
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task SetEnabledAsync(bool enabled, CancellationToken ct = default);
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);
    Task SetStringAsync(string key, string? value, CancellationToken ct = default);
}

public class AiFeatureFlagService : IAiFeatureFlagService
{
    private const string EnabledCacheKey = "ai:feature-flag:enabled";
    private const string FlagKey = "ai.enabled";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly AiOptions _options;

    public AiFeatureFlagService(AppDbContext db, IMemoryCache cache, IOptions<AiOptions> options)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<bool>(EnabledCacheKey, out var cached)) return cached;

        var flag = await _db.Set<FeatureFlag>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == FlagKey, ct);

        var effective = flag?.Enabled ?? _options.Enabled;
        _cache.Set(EnabledCacheKey, effective, CacheTtl);
        return effective;
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        var flag = await _db.Set<FeatureFlag>().FirstOrDefaultAsync(f => f.Key == FlagKey, ct);
        if (flag is null)
        {
            flag = new FeatureFlag { Key = FlagKey, Enabled = enabled, UpdatedAt = DateTime.UtcNow };
            _db.Add(flag);
        }
        else
        {
            flag.Enabled = enabled;
            flag.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        _cache.Remove(EnabledCacheKey);
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = StringCacheKey(key);
        if (_cache.TryGetValue<string?>(cacheKey, out var cached)) return cached;

        var flag = await _db.Set<FeatureFlag>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == key, ct);

        var value = flag?.Value;
        _cache.Set(cacheKey, value, CacheTtl);
        return value;
    }

    public async Task SetStringAsync(string key, string? value, CancellationToken ct = default)
    {
        var flag = await _db.Set<FeatureFlag>().FirstOrDefaultAsync(f => f.Key == key, ct);
        if (flag is null)
        {
            flag = new FeatureFlag { Key = key, Value = value, UpdatedAt = DateTime.UtcNow };
            _db.Add(flag);
        }
        else
        {
            flag.Value = value;
            flag.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        _cache.Remove(StringCacheKey(key));
    }

    private static string StringCacheKey(string key) => $"ai:feature-flag:value:{key}";
}
