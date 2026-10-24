using Microsoft.Extensions.Caching.Memory;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Derlenmiş stylesheet önbelleği: kendi <see cref="MemoryCache"/> örneği (uygulamanın paylaşılan
/// <c>IMemoryCache</c>'ine boyut sınırı dayatmamak için), en fazla <c>entries</c> giriş, anahtar
/// <c>(motor, SHA-256(xslt))</c>. Yalnız BAŞARILI derlemeler eklenir. Derlenmiş nesneler thread-safe'tir
/// (<c>XslCompiledTransform</c>, Saxon <c>XsltExecutable</c>).
/// </summary>
internal sealed class InProcessCompileCache : IDisposable
{
    private static readonly TimeSpan Sliding = TimeSpan.FromMinutes(30);

    private readonly MemoryCache _cache;

    public InProcessCompileCache(int entries)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(entries, 1);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = entries, CompactionPercentage = 0.25 });
    }

    public int Count => _cache.Count;

    public bool TryGet(XsltEngineKind engine, string sha256, out ICompiledXslt compiled)
    {
        if (_cache.TryGetValue(new Key(engine, sha256), out var value) && value is ICompiledXslt hit)
        {
            compiled = hit;
            return true;
        }

        compiled = null!;
        return false;
    }

    /// <summary>Doluysa giriş reddedilebilir (MemoryCache arka planda sıkıştırır) — önbellek en iyi çabadır.</summary>
    public void Add(XsltEngineKind engine, string sha256, ICompiledXslt compiled) =>
        _cache.Set(new Key(engine, sha256), compiled, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = Sliding });

    public void Dispose() => _cache.Dispose();

    private readonly record struct Key(XsltEngineKind Engine, string Sha256);
}
