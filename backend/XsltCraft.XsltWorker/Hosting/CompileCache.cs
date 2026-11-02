using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Derleme önbelleği anahtarı. <see cref="Sha256"/> worker'ın gövdeden KENDİ hesapladığı özettir (küçük harf hex);
/// <see cref="OptionsVersion"/> derlemeyi etkileyen ayarları taşır (stylesheet derinlik sınırı + motor ayar sürümü) —
/// gevşek sınırla derlenmiş bir executable daha sıkı sınırlı bir isteğe verilmesin.
/// </summary>
internal readonly record struct CompileKey(XsltEngineKind Engine, string Sha256, string OptionsVersion)
{
    /// <summary>Motor/derleyici ayarları değişirse artırılır (eski girdiler eşleşmez).</summary>
    public const int EngineOptionsVersion = 1;

    public static CompileKey For(XsltEngineKind engine, string sha256, XsltExecutionLimits limits) =>
        new(engine, sha256, $"v{EngineOptionsVersion};depth={limits.MaxStylesheetDepth}");
}

/// <summary>
/// Süreç başına LRU derleme önbelleği (varsayılan 16 giriş). Yalnız BAŞARILI derlemeler eklenir; böylece
/// <c>validate</c> ardından gelen <c>render</c> aynı executable'ı kullanır. Derlenmiş stylesheet'ler
/// (<see cref="ICompiledXslt"/>) thread-safe'tir; önbellek yalnız motor thread'inden kullanılır ama yine de kilitlidir.
/// </summary>
internal sealed class CompileCache
{
    public const int DefaultCapacity = 16;

    private readonly int _capacity;
    private readonly Dictionary<CompileKey, LinkedListNode<(CompileKey Key, ICompiledXslt Value)>> _map = [];
    private readonly LinkedList<(CompileKey Key, ICompiledXslt Value)> _order = new();
    private readonly object _gate = new();

    public CompileCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _map.Count;
        }
    }

    public bool TryGet(CompileKey key, out ICompiledXslt compiled)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                compiled = node.Value.Value;
                return true;
            }
        }

        compiled = null!;
        return false;
    }

    public void Add(CompileKey key, ICompiledXslt compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }

            _map[key] = _order.AddFirst((key, compiled));
            while (_map.Count > _capacity && _order.Last is { } last)
            {
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }

    /// <summary>Tüm girdileri bırakır (ör. bellek tükenmesinde GC'nin geri alabilmesi için).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
