namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>Worker başına son N XSLT özetinin LRU kümesi (yumuşak afinite). Havuz kilidi altında kullanılır.</summary>
internal sealed class HashAffinity
{
    private readonly int _capacity;
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _index = new(StringComparer.Ordinal);

    public HashAffinity(int capacity)
    {
        _capacity = capacity;
    }

    public int Count => _index.Count;

    public bool Contains(string hash) => _index.ContainsKey(hash);

    public void Touch(string hash)
    {
        if (_capacity <= 0)
            return;

        if (_index.TryGetValue(hash, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            return;
        }

        _index[hash] = _order.AddFirst(hash);
        if (_index.Count > _capacity)
        {
            var last = _order.Last!;
            _order.RemoveLast();
            _index.Remove(last.Value);
        }
    }
}
