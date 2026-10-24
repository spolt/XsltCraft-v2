using Microsoft.Extensions.Logging;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Infrastructure.Xslt.InProcess;

/// <summary>
/// <see cref="IXsltGuardObserver"/> → log (+ isteğe bağlı yeniden başlatma). Loglar yalnız xsltHash (ilk 16 hex),
/// kullanıcı, motor ve süre taşır — İÇERİK YOK. Takılı kaçak her zaman Critical; <c>restartRequested</c>
/// (<c>Xslt:Grid:RestartOnStuckRunaway</c>, SAHİP KARARI varsayılan <c>false</c>) ise <paramref name="requestRestart"/>
/// (Api'de <c>IHostApplicationLifetime.StopApplication</c>; konteyner yeniden başlatır) çağrılır.
/// </summary>
public sealed class XsltGuardObserver(ILogger<XsltGuardObserver> logger, Action? requestRestart = null) : IXsltGuardObserver
{
    public void Rejected(XsltGuardTicket ticket, TimeSpan waited) =>
        logger.LogWarning(
            "Süreç içi XSLT slotu yok (Busy): xsltHash={XsltHash} kullanıcı={UserId} motor={Engine} bekleme={WaitedMs:F0}ms",
            ticket.XsltHash, ticket.UserId, ticket.Engine, waited.TotalMilliseconds);

    public void RunawayStarted(XsltGuardTicket ticket, TimeSpan elapsed, bool timedOut, int runaways) =>
        logger.LogWarning(
            "Süreç içi XSLT kaçak thread ({Reason}): xsltHash={XsltHash} kullanıcı={UserId} motor={Engine} süre={ElapsedMs:F0}ms kaçak={Runaways}",
            timedOut ? "süre aşımı" : "iptal", ticket.XsltHash, ticket.UserId, ticket.Engine, elapsed.TotalMilliseconds, runaways);

    public void RunawayFinished(XsltGuardTicket ticket, TimeSpan elapsed, int runaways) =>
        logger.LogInformation(
            "Süreç içi XSLT kaçak thread bitti: xsltHash={XsltHash} kullanıcı={UserId} motor={Engine} süre={ElapsedMs:F0}ms kaçak={Runaways}",
            ticket.XsltHash, ticket.UserId, ticket.Engine, elapsed.TotalMilliseconds, runaways);

    public void RunawayStuck(XsltGuardTicket ticket, TimeSpan elapsed, int runaways, bool restartRequested)
    {
        logger.LogCritical(
            "Süreç içi XSLT thread'i takılı: xsltHash={XsltHash} kullanıcı={UserId} motor={Engine} süre={ElapsedMin:F1}dk kaçak={Runaways} yenidenBaşlat={Restart}",
            ticket.XsltHash, ticket.UserId, ticket.Engine, elapsed.TotalMinutes, runaways, restartRequested);

        if (restartRequested && requestRestart is not null)
            requestRestart();
    }

    public void DegradedChanged(bool degraded, int runaways)
    {
        if (degraded)
            logger.LogWarning("Süreç içi XSLT bozulmuş moda geçti (kaçak={Runaways}): grid istekleri worker'a yönleniyor.", runaways);
        else
            logger.LogInformation("Süreç içi XSLT bozulmuş moddan çıktı (kaçak={Runaways}).", runaways);
    }
}
