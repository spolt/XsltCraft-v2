using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary><see cref="XsltExecutionGuard.RunAsync{T}"/> sonucu sınıfı.</summary>
public enum XsltGuardStatus
{
    /// <summary>İş süre içinde bitti; <see cref="XsltGuardResult{T}.Value"/> dolu.</summary>
    Completed,

    /// <summary><see cref="XsltGridOptions.QueueWaitMs"/> içinde slot alınamadı (iş hiç başlamadı).</summary>
    Busy,

    /// <summary>Süre aşıldı; iş arka planda sürüyor (kaçak) ve slotunu bitene dek tutuyor.</summary>
    Timeout,
}

/// <summary>
/// Guard sonucu. <see cref="Elapsed"/> kuyruk beklemesi dahil toplam süredir. <see cref="Runaway"/> yalnız
/// <see cref="XsltGuardStatus.Timeout"/>'ta dolu: kaçak iş thread'i bitince tamamlanır.
/// </summary>
public readonly record struct XsltGuardResult<T>(XsltGuardStatus Status, T? Value, TimeSpan Elapsed, Task? Runaway = null);

/// <summary>
/// İstemci iptali işi kaçağa çevirdi (iş arka planda sürüyor, slotunu tutuyor). Normal iptalden ayrılır: kesici
/// bunu süreç içi Timeout sayar (yoksa iptal tekrarıyla bozulmuş mod süresiz tutulabilir — G2 reviewer Y1) ve
/// kullanıcı izni <see cref="RunawayFinished"/>'a dek tutulur (kullanıcı başı eşzamanlılık delinmez).
/// </summary>
public sealed class XsltRunawayCanceledException(Task runawayFinished, CancellationToken token)
    : OperationCanceledException("XSLT isteği iptal edildi; iş arka planda sürüyor.", token)
{
    /// <summary>Kaçak iş thread'i bitince tamamlanır (asla hata ile bitmez).</summary>
    public Task RunawayFinished { get; } = runawayFinished;
}

/// <summary>
/// Log/metrik kimliği — İÇERİK TAŞIMAZ: stylesheet SHA-256'sının ilk 16 hex'i, kullanıcı ve motor.
/// </summary>
public sealed record XsltGuardTicket(string XsltHash, Guid UserId, XsltEngineKind Engine);

/// <summary>
/// Guard olay portu (Application loglama/metrik/hosting bilmez). Uygulama Infrastructure'dadır:
/// log, metrik ve <see cref="RunawayStuck"/>'ta <c>restartRequested</c> ise uygulamayı durdurma.
/// Çağrılar guard kilitleri DIŞINDA yapılır; uygulamalar hızlı olmalı ve fırlatmamalıdır.
/// </summary>
public interface IXsltGuardObserver
{
    /// <summary>Slot alınamadı (<see cref="XsltGuardStatus.Busy"/>).</summary>
    void Rejected(XsltGuardTicket ticket, TimeSpan waited);

    /// <summary>Çağıran beklemeyi bıraktı (süre aşımı ya da iptal), iş sürüyor.</summary>
    void RunawayStarted(XsltGuardTicket ticket, TimeSpan elapsed, bool timedOut, int runaways);

    /// <summary>Kaçak iş sonunda bitti, slotu bırakıldı.</summary>
    void RunawayFinished(XsltGuardTicket ticket, TimeSpan elapsed, int runaways);

    /// <summary>
    /// Kaçak <see cref="XsltGridOptions.StuckRunawayMinutes"/>'ı aştı (Critical). <paramref name="restartRequested"/>
    /// = <see cref="XsltGridOptions.RestartOnStuckRunaway"/>; <c>true</c> ise uygulama durdurulmalıdır.
    /// </summary>
    void RunawayStuck(XsltGuardTicket ticket, TimeSpan elapsed, int runaways, bool restartRequested);

    /// <summary>Bozulmuş mod (kaçak ≥ <see cref="XsltGridOptions.RunawayMax"/>) girişi/çıkışı — yalnız bilgi.</summary>
    void DegradedChanged(bool degraded, int runaways);
}

/// <summary>Hiçbir şey yapmayan gözlemci (test / varsayılan).</summary>
public sealed class NullXsltGuardObserver : IXsltGuardObserver
{
    public static NullXsltGuardObserver Instance { get; } = new();

    public void Rejected(XsltGuardTicket ticket, TimeSpan waited)
    {
    }

    public void RunawayStarted(XsltGuardTicket ticket, TimeSpan elapsed, bool timedOut, int runaways)
    {
    }

    public void RunawayFinished(XsltGuardTicket ticket, TimeSpan elapsed, int runaways)
    {
    }

    public void RunawayStuck(XsltGuardTicket ticket, TimeSpan elapsed, int runaways, bool restartRequested)
    {
    }

    public void DegradedChanged(bool degraded, int runaways)
    {
    }
}
