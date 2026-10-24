namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Worker stderr'inin son N baytını tutan sabit boyutlu halka tampon. Akış sürekli boşaltılır
/// (dolu boru worker'ı bloklamasın); içerik ASLA loglanmaz — yalnız toplam bayt sayısı raporlanır.
/// </summary>
internal sealed class StderrRingBuffer
{
    private readonly byte[] _buffer;
    private readonly object _gate = new();
    private int _start;
    private int _count;
    private long _totalBytes;

    public StderrRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _buffer = new byte[capacity];
    }

    public int Capacity => _buffer.Length;

    public long TotalBytes
    {
        get { lock (_gate) return _totalBytes; }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            _totalBytes += data.Length;
            if (data.Length >= _buffer.Length)
            {
                data[^_buffer.Length..].CopyTo(_buffer);
                _start = 0;
                _count = _buffer.Length;
                return;
            }

            foreach (var b in data)
            {
                var index = (_start + _count) % _buffer.Length;
                _buffer[index] = b;
                if (_count < _buffer.Length)
                    _count++;
                else
                    _start = (_start + 1) % _buffer.Length;
            }
        }
    }

    /// <summary>Tutulan son baytlar (en eski → en yeni). Yalnız tanı/test için.</summary>
    public byte[] Snapshot()
    {
        lock (_gate)
        {
            var copy = new byte[_count];
            for (var i = 0; i < _count; i++)
                copy[i] = _buffer[(_start + i) % _buffer.Length];
            return copy;
        }
    }
}
