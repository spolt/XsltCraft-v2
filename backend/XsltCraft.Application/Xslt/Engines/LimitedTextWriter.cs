using System.Diagnostics;
using System.Text;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Çıktıyı iç yazıcıya aktarırken karakter sayısını ve süre sınırını denetler. Sınır aşılırsa
/// iç yazıcıya hiçbir şey yazmadan <see cref="XsltProcessingException"/> fırlatır
/// (<see cref="XsltFailureKind.OutputLimit"/> / <see cref="XsltFailureKind.Timeout"/>; iptal de
/// <see cref="XsltFailureKind.Timeout"/>, kod <see cref="CancelledCode"/>). İç yazıcıyı dispose etmez.
/// </summary>
public sealed class LimitedTextWriter : TextWriter
{
    /// <summary>İptal teşhisinin hata kodu (Saxon <c>LimitCodes.Cancelled</c> ile aynı anlam).</summary>
    public const string CancelledCode = "LimitCancelled";

    private readonly TextWriter _inner;
    private readonly long _maxChars;
    private readonly long _deadlineTimestamp;
    private readonly XsltEngineKind? _engine;
    private readonly CancellationToken _cancellation;
    private long _written;

    /// <param name="cancellation">İşbirlikçi iptal (<see cref="XsltExecutionLimits.Cancellation"/>): her yazımda denetlenir.</param>
    public LimitedTextWriter(TextWriter inner, long maxChars, TimeSpan deadline, XsltEngineKind? engine = null,
        CancellationToken cancellation = default)
        : base((inner ?? throw new ArgumentNullException(nameof(inner))).FormatProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxChars);
        _inner = inner;
        _maxChars = maxChars;
        _engine = engine;
        _cancellation = cancellation;
        _deadlineTimestamp = ComputeDeadline(deadline);
    }

    /// <summary>Şu ana dek iç yazıcıya aktarılan karakter sayısı.</summary>
    public long CharsWritten => _written;

    // Kodlama iç yazıcıdan gelir: XslCompiledTransform XML bildirimindeki encoding'i buradan alır.
    public override Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        Reserve(1);
        _inner.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Reserve(count);
        _inner.Write(buffer, index, count);
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        Reserve(buffer.Length);
        _inner.Write(buffer);
    }

    public override void Write(string? value)
    {
        if (value is null)
            return;
        Reserve(value.Length);
        _inner.Write(value);
    }

    public override void Flush() => _inner.Flush();

    private void Reserve(int count)
    {
        if (_cancellation.IsCancellationRequested)
            throw Fail(XsltFailureKind.Timeout, "XSLT dönüşümü iptal edildi.", CancelledCode);

        if (Stopwatch.GetTimestamp() > _deadlineTimestamp)
            throw Fail(XsltFailureKind.Timeout, "XSLT dönüşümü süre sınırını aştı.");

        if (_written + count > _maxChars)
            throw Fail(XsltFailureKind.OutputLimit, $"Çıktı boyutu sınırı aşıldı (en fazla {_maxChars} karakter).");

        _written += count;
    }

    // Sıfır/negatif/sonsuz → süre denetimi yok; taşma riski olan çok büyük süreler de öyle.
    private static long ComputeDeadline(TimeSpan deadline)
    {
        if (deadline <= TimeSpan.Zero)
            return long.MaxValue;

        var now = Stopwatch.GetTimestamp();
        var ticks = deadline.TotalSeconds * Stopwatch.Frequency;
        return ticks >= long.MaxValue - now ? long.MaxValue : now + (long)ticks;
    }

    private XsltProcessingException Fail(XsltFailureKind kind, string message, string? code = null) =>
        new(new XsltDiagnostic(message, 0, 0, kind, _engine, code));
}
