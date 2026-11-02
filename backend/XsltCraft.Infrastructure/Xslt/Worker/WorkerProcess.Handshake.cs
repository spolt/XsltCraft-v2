using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

// Başlatma el sıkışması: Hello çerçevesi, protokol sürümü ve PID denetimi.
internal sealed partial class WorkerProcess
{
    private async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.StartupTimeoutMs);

        var read = Task.Run(() => FrameCodec.ReadAsync(_stdout, FrameLimits.MaxResponseFrameBytes).AsTask());
        var completed = await Task.WhenAny(read, Task.Delay(Timeout.Infinite, deadline.Token)).ConfigureAwait(false);
        if (completed != read)
        {
            await KillAsync(WorkerExchangeStatus.Timeout).ConfigureAwait(false);
            Observe(read);
            throw new WorkerStartException(WorkerExitReasons.HandshakeFailed,
                cancellationToken.IsCancellationRequested ? "Havuz kapanırken başlatma iptal edildi." : "Worker Hello göndermedi (zaman aşımı).");
        }

        Frame? frame;
        try
        {
            frame = await read.ConfigureAwait(false);
        }
        catch (FrameProtocolException ex) when (ex.Error != FrameError.Truncated)
        {
            await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
            throw new WorkerStartException(WorkerExitReasons.ProtocolViolation, $"Hello çerçevesi bozuk: {ex.Error}.");
        }
        catch (Exception ex) when (ex is FrameProtocolException or IOException or ObjectDisposedException)
        {
            frame = null;
        }

        if (frame is null)
        {
            await WaitForExitAsync(TimeSpan.FromMilliseconds(_options.ShutdownGraceMs)).ConfigureAwait(false);
            throw new WorkerStartException(WorkerExitReasons.HandshakeFailed,
                $"Worker el sıkışmadan çıktı (çıkış kodu {ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}).");
        }

        HelloHeader hello;
        try
        {
            if (frame.Type != FrameType.Hello)
                throw new FrameProtocolException(FrameError.UnknownType, $"Hello beklenirken {frame.Type}.");
            hello = WorkerWire.DecodeHeader(frame, WorkerJsonContext.Default.HelloHeader);
        }
        catch (FrameProtocolException ex)
        {
            await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
            throw new WorkerStartException(WorkerExitReasons.ProtocolViolation, $"Hello geçersiz: {ex.Error}.");
        }

        if (hello.Protocol != ProtocolVersion.Current)
        {
            await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
            throw new WorkerStartException(WorkerExitReasons.ProtocolMismatch,
                $"Worker protokolü {hello.Protocol}; beklenen {ProtocolVersion.Current}.");
        }

        if (hello.Pid != Pid)
        {
            await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
            throw new WorkerStartException(WorkerExitReasons.ProtocolViolation, "Hello PID'i süreçle eşleşmiyor.");
        }

        Hello = hello;
    }
}
