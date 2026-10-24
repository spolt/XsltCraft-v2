using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Infrastructure.Xslt;

/// <summary>
/// Kullanıcı başı XSLT kapısı (ADR 008 §4, §7; <c>AiVisionThrottle</c> deseni, singleton).
/// <list type="bullet">
/// <item>Şerit başı eşzamanlılık (<see cref="XsltUserGateOptions"/>; varsayılan her şerit 1) → <c>UserBusy</c>.</item>
/// <item>Kötüye kullanım kesicisi, yürütme yerine göre ayrı: izole yerde pencere içinde
///   ≥ <see cref="XsltUserGateOptions.IsolatedAbuseThreshold"/> Timeout/EngineCrashed/MemoryLimit,
///   süreç içinde ≥ <see cref="XsltUserGateOptions.InProcessAbuseThreshold"/> Timeout →
///   o yer kullanıcıya <see cref="XsltUserGateOptions.BlockMinutes"/> kapalı (<c>Retry-After</c>).</item>
/// <item>Bellek sınırlı: boşta (izin yok, pencerede ihlal yok, blok yok) kullanıcı kayıtları
///   en geç dakikada bir süpürülür; yalnız son pencerede etkin kullanıcılar tutulur.</item>
/// </list>
/// Tek kilit: kritik bölümler mikro saniyedir; önizleme trafiğinde çekişme önemsizdir.
/// </summary>
public sealed class XsltUserGate : IXsltUserGate
{
    internal const string BusyCode = "UserBusy";
    internal const string BlockedCode = "UserBlocked";

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private static readonly int LaneCount = Enum.GetValues<XsltGateLane>().Length;
    private static readonly int PlaceCount = Enum.GetValues<XsltExecutionPlace>().Length;

    private readonly XsltUserGateOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<XsltUserGate> _logger;
    private readonly TimeSpan _window;
    private readonly TimeSpan _block;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, UserState> _users = [];
    private DateTimeOffset _nextSweep;

    public XsltUserGate(IOptions<XsltExecutionOptions> options, TimeProvider timeProvider, ILogger<XsltUserGate> logger)
    {
        _options = options.Value.UserGate;
        _time = timeProvider;
        _logger = logger;
        _window = TimeSpan.FromMinutes(_options.AbuseWindowMinutes);
        _block = TimeSpan.FromMinutes(_options.BlockMinutes);
        _nextSweep = _time.GetUtcNow() + SweepInterval;
    }

    /// <summary>Takip edilen kullanıcı sayısı (tanı/test).</summary>
    internal int TrackedUsers
    {
        get
        {
            lock (_lock)
                return _users.Count;
        }
    }

    public bool TryAcquire(
        Guid userId,
        XsltGateLane lane,
        XsltExecutionPlace place,
        [NotNullWhen(true)] out IXsltGateLease? lease,
        [NotNullWhen(false)] out XsltGateDenial? denial)
    {
        var max = _options.MaxConcurrentFor(lane);
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            SweepIfDueLocked(now);
            if (!_users.TryGetValue(userId, out var state))
            {
                state = new UserState();
                _users[userId] = state;
            }

            var blockedUntil = state.BlockedUntil[(int)place];
            if (blockedUntil > now)
            {
                lease = null;
                denial = new XsltGateDenial(
                    "Çok sayıda XSLT isteği süre/bellek sınırını aştı; bir süre sonra tekrar deneyin.",
                    BlockedCode, blockedUntil - now);
                return false;
            }

            if (state.InFlight[(int)lane] >= max)
            {
                lease = null;
                denial = new XsltGateDenial("Önceki XSLT isteğiniz sürüyor; tamamlanınca tekrar deneyin.", BusyCode, null);
                return false;
            }

            state.InFlight[(int)lane]++;
        }

        lease = new Lease(this, userId, lane, place);
        denial = null;
        return true;
    }

    // ------------------------------------------------------------------ izin yaşam döngüsü

    private void Release(Guid userId, XsltGateLane lane)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(userId, out var state) && state.InFlight[(int)lane] > 0)
                state.InFlight[(int)lane]--;
        }
    }

    private void Report(Guid userId, XsltExecutionPlace place, XsltFailureKind? failure)
    {
        if (failure is null || !CountsAsAbuse(place, failure.Value))
            return;

        var now = _time.GetUtcNow();
        var threshold = place == XsltExecutionPlace.Isolated ? _options.IsolatedAbuseThreshold : _options.InProcessAbuseThreshold;
        bool blocked;
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var state))
                return;

            var strikes = state.Strikes[(int)place];
            Prune(strikes, now - _window);
            strikes.Enqueue(now);
            blocked = strikes.Count >= threshold;
            if (blocked)
            {
                strikes.Clear();
                state.BlockedUntil[(int)place] = now + _block;
            }
        }

        if (blocked)
        {
            _logger.LogWarning(
                "XSLT kullanıcı kesicisi açıldı: kullanıcı={UserId} yer={Place} son={Failure} süre={BlockMinutes}dk",
                userId, place, failure, _options.BlockMinutes);
        }
    }

    internal static bool CountsAsAbuse(XsltExecutionPlace place, XsltFailureKind failure) => place switch
    {
        XsltExecutionPlace.Isolated => failure is XsltFailureKind.Timeout or XsltFailureKind.EngineCrashed or XsltFailureKind.MemoryLimit,
        _ => failure is XsltFailureKind.Timeout,
    };

    // ------------------------------------------------------------------ bellek sınırı

    private void SweepIfDueLocked(DateTimeOffset now)
    {
        if (now < _nextSweep)
            return;

        _nextSweep = now + SweepInterval;
        var cutoff = now - _window;
        List<Guid>? idle = null;
        foreach (var (userId, state) in _users)
        {
            if (state.IsIdle(now, cutoff))
                (idle ??= []).Add(userId);
        }

        if (idle is null)
            return;
        foreach (var userId in idle)
            _users.Remove(userId);
    }

    private static void Prune(Queue<DateTimeOffset> strikes, DateTimeOffset cutoff)
    {
        while (strikes.Count > 0 && strikes.Peek() <= cutoff)
            strikes.Dequeue();
    }

    private sealed class UserState
    {
        public int[] InFlight { get; } = new int[LaneCount];

        public Queue<DateTimeOffset>[] Strikes { get; } = Enumerable.Range(0, PlaceCount).Select(_ => new Queue<DateTimeOffset>()).ToArray();

        public DateTimeOffset[] BlockedUntil { get; } = new DateTimeOffset[PlaceCount];

        public bool IsIdle(DateTimeOffset now, DateTimeOffset cutoff)
        {
            if (InFlight.Any(n => n > 0) || BlockedUntil.Any(b => b > now))
                return false;

            foreach (var strikes in Strikes)
            {
                Prune(strikes, cutoff);
                if (strikes.Count > 0)
                    return false;
            }

            return true;
        }
    }

    private sealed class Lease(XsltUserGate gate, Guid userId, XsltGateLane lane, XsltExecutionPlace place) : IXsltGateLease
    {
        private int _reported;
        private int _disposed;

        public void ReportOutcome(XsltFailureKind? failure, XsltExecutionPlace? actualPlace = null)
        {
            if (Interlocked.Exchange(ref _reported, 1) == 0 && Volatile.Read(ref _disposed) == 0)
                gate.Report(userId, actualPlace ?? place, failure);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gate.Release(userId, lane);
        }
    }
}
