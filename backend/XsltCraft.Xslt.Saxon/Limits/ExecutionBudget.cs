using System.Diagnostics;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Limits;

/// <summary>
/// Tek bir dönüşümün süre + iptal bütçesi ve ilk sınır ihlalinin kaydı. Listener ve yazıcı paylaşır;
/// ilk ihlal YAPIŞKANDIR: <see cref="Tripped"/> dolduktan sonra her <see cref="Check"/> aynı hatayı
/// yeniden fırlatır. Transform başına bir örnek, tek thread (thread-safe değildir).
/// </summary>
internal sealed class ExecutionBudget
{
    private readonly long _deadlineTimestamp;
    private readonly CancellationToken _cancellationToken;

    public ExecutionBudget(TimeSpan deadline, CancellationToken cancellationToken)
    {
        _deadlineTimestamp = ComputeDeadline(deadline);
        _cancellationToken = cancellationToken;
    }

    /// <summary>İlk sınır ihlali (sonrakiler bu nesneyi yeniden fırlatır).</summary>
    public SaxonLimitError? Tripped { get; private set; }

    /// <summary>Önceki ihlal varsa onu, yoksa iptal / süre ihlalini fırlatır.</summary>
    public void Check()
    {
        if (Tripped is { } tripped)
            throw tripped;

        if (_cancellationToken.IsCancellationRequested)
            throw Trip(new SaxonLimitError(XsltFailureKind.Timeout, LimitCodes.Cancelled, "XSLT dönüşümü iptal edildi."));

        if (Stopwatch.GetTimestamp() > _deadlineTimestamp)
            throw Trip(new SaxonLimitError(XsltFailureKind.Timeout, LimitCodes.Deadline, "XSLT dönüşümü süre sınırını aştı."));
    }

    /// <summary>İhlali kaydeder (yalnız ilki saklanır) ve fırlatılacak hatayı döner.</summary>
    public SaxonLimitError Trip(SaxonLimitError error)
    {
        Tripped ??= error;
        return Tripped;
    }

    // LimitedTextWriter ile aynı kural: sıfır/negatif/sonsuz → süre denetimi yok.
    private static long ComputeDeadline(TimeSpan deadline)
    {
        if (deadline <= TimeSpan.Zero)
            return long.MaxValue;

        var now = Stopwatch.GetTimestamp();
        var ticks = deadline.TotalSeconds * Stopwatch.Frequency;
        return ticks >= long.MaxValue - now ? long.MaxValue : now + (long)ticks;
    }
}
