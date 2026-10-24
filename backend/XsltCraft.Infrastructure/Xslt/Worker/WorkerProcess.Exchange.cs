using System.ComponentModel;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

// İstek/yanıt alışverişi: sert zaman aşımı, WorkingSet bekçisi ve kapanan akışın sınıflandırılması.
internal sealed partial class WorkerProcess
{
    /// <summary>
    /// Bir istek gönderir, yanıtı bekler. <paramref name="timeout"/> sert sınırdır; dolduğunda süreç
    /// ağacı öldürülür. Çağıranın iptali bilinçli olarak YOKTUR (istek iptali worker'ı öldürmez).
    /// </summary>
    public async Task<WorkerExchange> ExecuteAsync(
        RequestHeader header,
        IReadOnlyList<ReadOnlyMemory<byte>> bodies,
        TimeSpan timeout)
    {
        using var stop = new CancellationTokenSource();

        // Task.Run: boru yazımı eşzamanlı bloklasa bile zamanlayıcı ve bekçi çalışmaya devam eder.
        var exchange = Task.Run(() => ExchangeAsync(header, bodies));
        var watchdog = WatchMemoryAsync(stop.Token);
        var timer = Task.Delay(timeout, stop.Token);

        var first = await Task.WhenAny(exchange, watchdog, timer).ConfigureAwait(false);
        if (first == timer && !exchange.IsCompleted)
            await KillAsync(WorkerExchangeStatus.Timeout).ConfigureAwait(false);

        await stop.CancelAsync().ConfigureAwait(false);
        await watchdog.ConfigureAwait(false);

        // Öldürülen sürecin borusu kapanır → okuma EOF ile biter. Yine de sınırlı bekle.
        if (await Task.WhenAny(exchange, Task.Delay(_options.ShutdownGraceMs)).ConfigureAwait(false) != exchange)
        {
            Observe(exchange);
            if (KillStatus is null)
                await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
            return Dead(KillStatus ?? WorkerExchangeStatus.ProtocolViolation, "exchange_stuck");
        }

        if (KillStatus is { } killed)
        {
            Observe(exchange);
            return Dead(killed, null);
        }

        Frame? frame;
        try
        {
            frame = await exchange.ConfigureAwait(false);
        }
        catch (RequestTooLargeException ex)
        {
            return new WorkerExchange(WorkerExchangeStatus.RequestTooLarge, null, null, ex.Message);
        }
        catch (FrameProtocolException ex) when (ex.Error != FrameError.Truncated)
        {
            return await ViolationAsync(ex.Error.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FrameProtocolException or IOException or ObjectDisposedException)
        {
            frame = null;
        }

        if (frame is null)
            return await ClassifyClosedStreamAsync().ConfigureAwait(false);

        if (frame.Type != FrameType.Response)
            return await ViolationAsync($"unexpected_{frame.Type}").ConfigureAwait(false);

        try
        {
            var response = WorkerWire.DecodeHeader(frame, WorkerJsonContext.Default.ResponseHeader);
            if (!string.Equals(response.Id, header.Id, StringComparison.Ordinal))
                return await ViolationAsync("id_mismatch").ConfigureAwait(false);

            var output = frame.Bodies.Count > 0 ? WorkerWire.DecodeBody(frame.Bodies[0]) : null;
            return new WorkerExchange(WorkerExchangeStatus.Completed, new WorkerResponse(response, output), null, null);
        }
        catch (FrameProtocolException ex)
        {
            return await ViolationAsync(ex.Error.ToString()).ConfigureAwait(false);
        }
    }

    private async Task<Frame?> ExchangeAsync(RequestHeader header, IReadOnlyList<ReadOnlyMemory<byte>> bodies)
    {
        try
        {
            await WorkerWire.WriteRequestAsync(_stdin, header, bodies).ConfigureAwait(false);
        }
        catch (FrameProtocolException ex)
        {
            // Codec sınır ihlalinde akışa hiç bayt yazmaz → worker sağlıklı kalır.
            throw new RequestTooLargeException(ex.Message);
        }

        return await FrameCodec.ReadAsync(_stdout, FrameLimits.MaxResponseFrameBytes).ConfigureAwait(false);
    }

    private async Task WatchMemoryAsync(CancellationToken cancellationToken)
    {
        var limit = _options.MaxWorkingSetMb * Megabyte;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.WorkingSetPollMs));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (limit <= 0)
                    continue;

                if (SampleWorkingSet() is { } workingSet && workingSet > limit)
                {
                    await KillAsync(WorkerExchangeStatus.MemoryLimit).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // İstek bitti.
        }
    }

    private long? SampleWorkingSet()
    {
        try
        {
            _process.Refresh();
            return _process.WorkingSet64;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private async Task<WorkerExchange> ClassifyClosedStreamAsync()
    {
        if (await WaitForExitAsync(TimeSpan.FromMilliseconds(_options.ShutdownGraceMs)).ConfigureAwait(false))
            return KillStatus is { } killed ? Dead(killed, null) : Dead(WorkerExchangeStatus.Crashed, null);

        return await ViolationAsync("stdout_closed").ConfigureAwait(false);
    }

    private async Task<WorkerExchange> ViolationAsync(string detail)
    {
        await KillAsync(WorkerExchangeStatus.ProtocolViolation).ConfigureAwait(false);
        return Dead(KillStatus ?? WorkerExchangeStatus.ProtocolViolation, detail);
    }

    private WorkerExchange Dead(WorkerExchangeStatus status, string? detail) => new(status, null, ExitCode, detail);

    private sealed class RequestTooLargeException(string message) : Exception(message);
}
