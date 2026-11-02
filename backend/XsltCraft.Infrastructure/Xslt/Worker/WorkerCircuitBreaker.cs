namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Kayan pencere kesici: <c>window</c> içinde <c>threshold</c> çökme → <c>openFor</c> boyunca açık.
/// Yalnız çökme/protokol ihlali/el sıkışma hatası sayılır; zaman aşımı ve bellek öldürmeleri
/// kullanıcı kaynaklıdır ve kullanıcı kapısının (UserGate) işidir. Havuz kilidi altında kullanılır.
/// </summary>
internal sealed class WorkerCircuitBreaker
{
    private readonly int _threshold;
    private readonly TimeSpan _window;
    private readonly TimeSpan _openFor;
    private readonly Queue<DateTimeOffset> _failures = new();
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public WorkerCircuitBreaker(int threshold, TimeSpan window, TimeSpan openFor)
    {
        _threshold = threshold;
        _window = window;
        _openFor = openFor;
    }

    public bool IsOpen(DateTimeOffset now) => now < _openUntil;

    /// <summary>Hatayı kaydeder; kesici bu çağrıyla açıldıysa <c>true</c>.</summary>
    public bool RecordFailure(DateTimeOffset now)
    {
        _failures.Enqueue(now);
        while (_failures.Count > 0 && now - _failures.Peek() > _window)
            _failures.Dequeue();

        if (_failures.Count < _threshold || IsOpen(now))
            return false;

        _openUntil = now + _openFor;
        _failures.Clear();
        return true;
    }
}
