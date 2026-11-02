using System.Globalization;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// Süreç içi (grid) XSLT yürütme koruması (ADR 008 §7). Singleton; Application loglama/hosting bilmez —
/// olaylar <see cref="IXsltGuardObserver"/> portuna gider.
/// <list type="bullet">
/// <item><b>Slot:</b> <see cref="XsltGridOptions.MaxConcurrent"/> semaforu; <see cref="XsltGridOptions.QueueWaitMs"/>
///   içinde alınamazsa <see cref="XsltGuardStatus.Busy"/> (iş başlamaz).</item>
/// <item><b>Thread:</b> her iş ayrı, <see cref="ThreadStackBytes"/> yığınlı arka plan thread'inde (derleme dahil —
///   derleme anı özyinelemesi istek thread'inin 1 MB yığınını taşırmasın).</item>
/// <item><b>Süre:</b> çağıran <c>WaitAsync(timeout, TimeProvider, ct)</c> ile bekler. Süre aşımında
///   <see cref="XsltGuardStatus.Timeout"/> döner; iş ARKA PLANDA SÜRER (thread öldürülemez) ve slotunu bitene dek
///   TUTAR. İşe verilen belirteç iptal edilir (işbirlikçi motorlar — Saxon bütçesi, .NET çıktı yazıcısı — durur);
///   durmayanlar kaçak (runaway) sayılır. İstemci iptalinde de aynı kural (slot kaçırılamaz).</item>
/// <item><b>Bozulmuş mod:</b> <see cref="IsDegraded"/> = kaçak ≥ <see cref="XsltGridOptions.RunawayMax"/>;
///   yönlendirme kararı host'undur. Kaçaklar bitince kendiliğinden kalkar.</item>
/// <item><b>Takılı kaçak:</b> <see cref="XsltGridOptions.StuckRunawayMinutes"/> sonra
///   <see cref="IXsltGuardObserver.RunawayStuck"/> (Critical; <see cref="XsltGridOptions.RestartOnStuckRunaway"/>
///   bayrağı gözlemciye iletilir — SAHİP KARARI varsayılan <c>false</c>).</item>
/// </list>
/// </summary>
public sealed class XsltExecutionGuard
{
    /// <summary>İş thread'i yığını (Saxon InProcessGrid profili 256 trace derinliği buna göre seçildi).</summary>
    public const int ThreadStackBytes = 16 * 1024 * 1024;

    private readonly XsltGridOptions _options;
    private readonly TimeProvider _time;
    private readonly IXsltGuardObserver _observer;
    private readonly SemaphoreSlim _slots;
    private readonly TimeSpan _queueWait;
    private readonly TimeSpan _stuckAfter;
    private int _active;
    private int _runaways;

    public XsltExecutionGuard(XsltGridOptions options, TimeProvider timeProvider, IXsltGuardObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var errors = new List<string>();
        options.CollectErrors(errors);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        _options = options;
        _time = timeProvider;
        _observer = observer ?? NullXsltGuardObserver.Instance;
        _slots = new SemaphoreSlim(options.MaxConcurrent, options.MaxConcurrent);
        _queueWait = TimeSpan.FromMilliseconds(options.QueueWaitMs);
        _stuckAfter = TimeSpan.FromMinutes(options.StuckRunawayMinutes);
    }

    public int MaxConcurrent => _options.MaxConcurrent;

    /// <summary>Çalışan iş thread'leri (kaçaklar dahil).</summary>
    public int ActiveCount => Volatile.Read(ref _active);

    /// <summary>Çağıranın bırakıp hâlâ çalışan işler.</summary>
    public int RunawayCount => Volatile.Read(ref _runaways);

    /// <summary>Kaçak ≥ <see cref="XsltGridOptions.RunawayMax"/>: yeni işler süreç içinde başlatılmamalı.</summary>
    public bool IsDegraded => RunawayCount >= _options.RunawayMax;

    /// <summary>
    /// <paramref name="work"/>'ü korumalı thread'de çalıştırır. <paramref name="work"/>'ün istisnası çağırana
    /// yeniden fırlatılır (yalnız süre içinde bittiyse). <paramref name="cancellationToken"/> iptali
    /// <see cref="OperationCanceledException"/> fırlatır; iş arka planda sürüyorsa kaçak sayılır ve istisna
    /// <see cref="XsltRunawayCanceledException"/>'dır (çağıran kesiciye Timeout bildirip kullanıcı iznini
    /// <see cref="XsltRunawayCanceledException.RunawayFinished"/>'a dek tutmalı). Süre aşımında da
    /// <see cref="XsltGuardResult{T}.Runaway"/> aynı görevi taşır.
    /// </summary>
    /// <param name="timeout">Slot alındıktan sonraki sert süre; ≤ 0 → sınırsız.</param>
    /// <param name="work">Belirteç yalnız çağıran bıraktığında (süre/iptal) iptal edilir.</param>
    public async Task<XsltGuardResult<T>> RunAsync<T>(
        XsltGuardTicket ticket,
        TimeSpan timeout,
        Func<CancellationToken, T> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(work);

        var started = _time.GetTimestamp();
        if (!await _slots.WaitAsync(_queueWait, cancellationToken).ConfigureAwait(false))
        {
            var waited = _time.GetElapsedTime(started);
            Notify(o => o.Rejected(ticket, waited));
            return new XsltGuardResult<T>(XsltGuardStatus.Busy, default, waited);
        }

        // Slot artık işe aittir: iş thread'i bitince bırakır (kaçak olsa da).
        var job = new Job<T>(this, ticket, work);
        job.Start();

        try
        {
            var value = await job.Completion
                .WaitAsync(timeout > TimeSpan.Zero ? timeout : Timeout.InfiniteTimeSpan, _time, cancellationToken)
                .ConfigureAwait(false);
            return new XsltGuardResult<T>(XsltGuardStatus.Completed, value, _time.GetElapsedTime(started));
        }
        catch (TimeoutException)
        {
            if (job.TryAbandon(timedOut: true))
                return new XsltGuardResult<T>(XsltGuardStatus.Timeout, default, _time.GetElapsedTime(started), job.Finished);

            // Yarış: iş süre dolarken bitti → sonucu kullan (iş kendi TimeoutException'ını attıysa o da fırlar).
            var value = await job.Completion.ConfigureAwait(false);
            return new XsltGuardResult<T>(XsltGuardStatus.Completed, value, _time.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (job.TryAbandon(timedOut: false))
                throw new XsltRunawayCanceledException(job.Finished, cancellationToken);
            throw;
        }
    }

    // ------------------------------------------------------------------ iş yaşam döngüsü

    private void OnRunawayStarted(XsltGuardTicket ticket, TimeSpan elapsed, bool timedOut, int runaways)
    {
        Notify(o => o.RunawayStarted(ticket, elapsed, timedOut, runaways));
        if (runaways == _options.RunawayMax)
            Notify(o => o.DegradedChanged(true, runaways));
    }

    /// <summary>İş thread'inin son adımı: önce sayaçlar + slot (asla atlanmaz), sonra gözlemci.</summary>
    private void OnFinished(XsltGuardTicket ticket, bool wasRunaway, TimeSpan elapsed)
    {
        var runaways = wasRunaway ? Interlocked.Decrement(ref _runaways) : RunawayCount;
        Interlocked.Decrement(ref _active);
        _slots.Release();

        if (!wasRunaway)
            return;

        Notify(o => o.RunawayFinished(ticket, elapsed, runaways));
        if (runaways == _options.RunawayMax - 1)
            Notify(o => o.DegradedChanged(false, runaways));
    }

    /// <summary>
    /// Gözlemci hatası yutulur: iş thread'inde fırlayan istisna süreci düşürür; istek yolunda ise sonucu bozmamalı.
    /// </summary>
    private void Notify(Action<IXsltGuardObserver> notify)
    {
        try
        {
            notify(_observer);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Bilinçli: raporlanacak başka kanal yok (Application loglama bilmez).
        }
    }

    private enum JobState
    {
        Running,
        Abandoned,
        Finished,
    }

    /// <summary>
    /// Tek iş. Durum geçişleri (Running → Abandoned → Finished / Running → Finished) <c>_lock</c> altında:
    /// kaçak sayacının artışı her zaman azalışından önce olur; iptal, belirteç kaynağı dispose edildikten sonra
    /// çağrılmaz.
    /// </summary>
    private sealed class Job<T>
    {
        private readonly XsltExecutionGuard _guard;
        private readonly XsltGuardTicket _ticket;
        private readonly Func<CancellationToken, T> _work;
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _abandon = new();
        private readonly object _lock = new();
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private JobState _state;
        private ITimer? _stuckTimer;
        private long _startTimestamp;
        private DateTimeOffset _startUtc;

        public Job(XsltExecutionGuard guard, XsltGuardTicket ticket, Func<CancellationToken, T> work)
        {
            _guard = guard;
            _ticket = ticket;
            _work = work;
        }

        public Task<T> Completion => _completion.Task;

        /// <summary>İş thread'i bitti (slot bırakıldı) — sonuçtan bağımsız, asla hata ile bitmez.</summary>
        public Task Finished => _finished.Task;

        public void Start()
        {
            _startTimestamp = _guard._time.GetTimestamp();
            _startUtc = _guard._time.GetUtcNow();
            Interlocked.Increment(ref _guard._active);
            try
            {
                new Thread(Run, ThreadStackBytes) { IsBackground = true, Name = "xslt-inproc" }.Start();
            }
            catch
            {
                // Thread açılamadı (OOM vb.): slotu geri ver, çağırana fırlat.
                lock (_lock)
                    _state = JobState.Finished;
                _abandon.Dispose();
                Interlocked.Decrement(ref _guard._active);
                _guard._slots.Release();
                throw;
            }
        }

        /// <summary>Çağıran bıraktı. İş zaten bittiyse <c>false</c> (kaçak yok).</summary>
        public bool TryAbandon(bool timedOut)
        {
            int runaways;
            lock (_lock)
            {
                if (_state != JobState.Running)
                    return false;

                _state = JobState.Abandoned;
                runaways = Interlocked.Increment(ref _guard._runaways);
                _abandon.Cancel();

                var due = _guard._stuckAfter - (_guard._time.GetUtcNow() - _startUtc);
                _stuckTimer = _guard._time.CreateTimer(_ => OnStuck(), null,
                    due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }

            // Kimse beklemiyor: işin istisnası gözlenmeli (UnobservedTaskException olmasın).
            _ = Completion.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            _guard.OnRunawayStarted(_ticket, _guard._time.GetElapsedTime(_startTimestamp), timedOut, runaways);
            return true;
        }

        private void Run()
        {
            try
            {
                CultureInfo.CurrentCulture = _culture;
                CultureInfo.CurrentUICulture = _uiCulture;
                _completion.TrySetResult(_work(_abandon.Token));
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
            }
            finally
            {
                Finish();
            }
        }

        private void Finish()
        {
            bool wasRunaway;
            ITimer? timer;
            lock (_lock)
            {
                wasRunaway = _state == JobState.Abandoned;
                _state = JobState.Finished;
                timer = _stuckTimer;
                _stuckTimer = null;
            }

            timer?.Dispose();
            _abandon.Dispose();
            try
            {
                _guard.OnFinished(_ticket, wasRunaway, _guard._time.GetElapsedTime(_startTimestamp));
            }
            finally
            {
                _finished.TrySetResult();
            }
        }

        private void OnStuck()
        {
            lock (_lock)
            {
                if (_state != JobState.Abandoned)
                    return;
            }

            var elapsed = _guard._time.GetUtcNow() - _startUtc;
            _guard.Notify(o => o.RunawayStuck(_ticket, elapsed, _guard.RunawayCount, _guard._options.RestartOnStuckRunaway));
        }
    }
}
