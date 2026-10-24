using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Limits;

/// <summary>
/// Saxon serializer'ının yazdığı <c>java.io.Writer</c>: karakterleri .NET <see cref="TextWriter"/>'a aktarır,
/// en fazla <c>maxChars</c> karaktere izin verir ve her yazımda süre bütçesini denetler. Sınır aşılırsa
/// o parça iç yazıcıya yazılmadan <see cref="SaxonLimitError"/> (OutputLimit) fırlatılır — IOException
/// değil, çünkü Saxon G/Ç hatalarını sarar ve hata türü kaybolur. İç yazıcıyı kapatmaz.
/// </summary>
internal sealed class LimitedJavaWriter : java.io.Writer
{
    private readonly TextWriter _inner;
    private readonly long _maxChars;
    private readonly ExecutionBudget _budget;
    private long _written;

    public LimitedJavaWriter(TextWriter inner, long maxChars, ExecutionBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxChars);
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxChars = maxChars;
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    public long CharsWritten => _written;

    public override void write(char[] cbuf, int off, int len)
    {
        Reserve(len);
        _inner.Write(cbuf, off, len);
    }

    public override void write(int c)
    {
        Reserve(1);
        _inner.Write((char)c);
    }

    public override void write(string str, int off, int len)
    {
        Reserve(len);
        _inner.Write(str.AsSpan(off, len));
    }

    public override void flush() => _inner.Flush();

    // Serializer close() çağırır; iç yazıcının sahibi çağıran taraftır.
    public override void close() => _inner.Flush();

    private void Reserve(int count)
    {
        _budget.Check();

        if (_written + count > _maxChars)
        {
            throw _budget.Trip(new SaxonLimitError(XsltFailureKind.OutputLimit, LimitCodes.Output,
                $"Çıktı boyutu sınırı aşıldı (en fazla {_maxChars} karakter)."));
        }

        _written += count;
    }
}
