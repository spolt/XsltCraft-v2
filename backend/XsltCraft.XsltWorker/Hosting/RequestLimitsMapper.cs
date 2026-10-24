using XsltCraft.Application.Xslt.Engines;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Tel sınırları (<see cref="RequestLimits"/>) → <see cref="XsltExecutionLimits"/>. Eksik/sıfır/negatif alan →
/// <see cref="XsltExecutionLimits.Default"/> değeri (sınırsız DEĞİL); geniş tipler int'e kıstırılır.
/// İşbirlikçi süre/iptal istek başında <see cref="CooperativeDeadline"/> ile kurulur.
/// </summary>
internal static class RequestLimitsMapper
{
    public static XsltExecutionLimits ToExecutionLimits(RequestLimits? wire)
    {
        var defaults = XsltExecutionLimits.Default;
        if (wire is null)
            return defaults;

        return defaults with
        {
            Deadline = wire.DeadlineMs > 0 ? TimeSpan.FromMilliseconds(wire.DeadlineMs) : defaults.Deadline,
            MaxOutputChars = Positive(wire.MaxOutputChars, defaults.MaxOutputChars),
            MaxStylesheetDepth = wire.MaxStylesheetDepth > 0 ? wire.MaxStylesheetDepth : defaults.MaxStylesheetDepth,
            MaxInputDepth = wire.MaxInputDepth > 0 ? wire.MaxInputDepth : defaults.MaxInputDepth,
            MaxInputNodes = Positive(wire.MaxInputNodes, defaults.MaxInputNodes),
        };
    }

    private static int Positive(long value, int fallback) =>
        value > 0 ? (int)Math.Min(value, int.MaxValue) : fallback;
}

/// <summary>
/// İstek başında başlayan işbirlikçi süre: derleme + dönüşümün toplamı <see cref="XsltExecutionLimits.Deadline"/>'ı
/// aşmamalı. Dönüşüme kalan süre <see cref="XsltExecutionLimits.Deadline"/> olarak, aynı anda dolan (küçük payla)
/// iptal belirteci <see cref="XsltExecutionLimits.Cancellation"/> olarak verilir — Saxon bütçesi ikisini de denetler,
/// .NET yazıcısı süreyi. Sert sınır Api'dedir (süreç öldürme).
/// </summary>
internal sealed class CooperativeDeadline : IDisposable
{
    // Süre ve iptal aynı anda dolarsa motor "süre aşıldı" mesajını tercih etsin diye iptal biraz geç tetiklenir.
    private static readonly TimeSpan CancellationGrace = TimeSpan.FromMilliseconds(100);

    private readonly TimeSpan _total;
    private readonly long _startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
    private readonly CancellationTokenSource _cts;

    public CooperativeDeadline(TimeSpan total)
    {
        _total = total;
        _cts = new CancellationTokenSource(total + CancellationGrace);
    }

    public TimeSpan Remaining => _total - System.Diagnostics.Stopwatch.GetElapsedTime(_startedAt);

    /// <summary>Dönüşüm sınırları; süre dolduysa <c>null</c>.</summary>
    public XsltExecutionLimits? ForTransform(XsltExecutionLimits limits)
    {
        var remaining = Remaining;
        if (remaining <= TimeSpan.Zero)
            return null;
        return limits with { Deadline = remaining, Cancellation = _cts.Token };
    }

    public void Dispose() => _cts.Dispose();
}
