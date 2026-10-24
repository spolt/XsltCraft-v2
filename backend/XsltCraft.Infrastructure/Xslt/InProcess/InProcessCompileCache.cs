using Microsoft.Extensions.Caching.Memory;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// Derlenmiş stylesheet önbelleği: kendi <see cref="MemoryCache"/> örneği (uygulamanın paylaşılan
/// <c>IMemoryCache</c>'ine boyut sınırı dayatmamak için). Boyut tabanlı (G2 #6: 6M karakterlik stylesheet derlenmiş
/// ~11.5 MB → 32 giriş ≈ 370 MB): giriş boyutu = stylesheet karakter sayısı, ama en az <c>maxTotalChars / entries</c>
/// (giriş sayısı da <c>entries</c>'i aşmaz). Anahtar <c>(motor, SHA-256(xslt), MaxStylesheetDepth)</c> — derinlik
/// sınırı farklı istek başka sınırla derlenmiş nesneyi kullanmasın. Yalnız BAŞARILI derlemeler eklenir.
/// Derlenmiş nesneler thread-safe'tir (<c>XslCompiledTransform</c>, Saxon <c>XsltExecutable</c>).
/// </summary>
internal sealed class InProcessCompileCache : IDisposable
{
    public const long DefaultMaxTotalChars = 64_000_000;

    private static readonly TimeSpan Sliding = TimeSpan.FromMinutes(30);

    private readonly MemoryCache _cache;
    private readonly long _minEntrySize;

    public InProcessCompileCache(int entries, long maxTotalChars = DefaultMaxTotalChars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(entries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalChars, entries);
        _minEntrySize = maxTotalChars / entries;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = maxTotalChars, CompactionPercentage = 0.25 });
    }

    public int Count => _cache.Count;

    public bool TryGet(XsltEngineKind engine, string sha256, int maxStylesheetDepth, out ICompiledXslt compiled)
    {
        if (_cache.TryGetValue(new Key(engine, sha256, maxStylesheetDepth), out var value) && value is ICompiledXslt hit)
        {
            compiled = hit;
            return true;
        }

        compiled = null!;
        return false;
    }

    /// <summary>Doluysa giriş reddedilebilir (MemoryCache arka planda sıkıştırır) — önbellek en iyi çabadır.</summary>
    public void Add(XsltEngineKind engine, string sha256, int maxStylesheetDepth, int stylesheetChars, ICompiledXslt compiled) =>
        _cache.Set(new Key(engine, sha256, maxStylesheetDepth), compiled, new MemoryCacheEntryOptions
        {
            Size = Math.Max(stylesheetChars, _minEntrySize),
            SlidingExpiration = Sliding,
        });

    public void Dispose() => _cache.Dispose();

    private readonly record struct Key(XsltEngineKind Engine, string Sha256, int MaxStylesheetDepth);
}
