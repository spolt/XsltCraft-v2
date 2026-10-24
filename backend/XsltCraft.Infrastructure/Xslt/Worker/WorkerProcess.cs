using System.ComponentModel;
using System.Diagnostics;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Tek bir worker süreci: başlatma + Hello denetimi, aynı anda TEK istek, stderr'in sınırlı halka
/// tampona boşaltılması, WorkingSet bekçisi ve sert zaman aşımı. Her arızada (zaman aşımı, bellek,
/// protokol ihlali) süreç ağacı öldürülür — worker içindeki yakalanamaz hatalar (StackOverflow) Api'yi
/// etkilemez. Thread-safe DEĞİL: havuz bir worker'a aynı anda tek istek verir.
/// </summary>
internal sealed class WorkerProcess : IAsyncDisposable
{
    private const int StderrCapacity = 8 * 1024;
    private const long Megabyte = 1024 * 1024;

    private readonly Process _process;
    private readonly WorkerLaunchSpec _spec;
    private readonly XsltWorkerOptions _options;
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private readonly Task _exited;
    private readonly Task _stderrPump;
    private readonly StderrRingBuffer _stderr = new(StderrCapacity);
    private int _killStatus = -1;
    private int _disposed;

    private WorkerProcess(Process process, WorkerLaunchSpec spec, XsltWorkerOptions options)
    {
        _process = process;
        _spec = spec;
        _options = options;
        Pid = process.Id;
        StartedAt = DateTimeOffset.UtcNow;
        _stdin = process.StandardInput.BaseStream;
        _stdout = process.StandardOutput.BaseStream;
        _exited = process.WaitForExitAsync();
        _stderrPump = Task.Run(() => PumpStderrAsync(process.StandardError.BaseStream));
    }

    public int Pid { get; }

    public DateTimeOffset StartedAt { get; }

    public HelloHeader? Hello { get; private set; }

    /// <summary>Süreç çıktığında tamamlanır.</summary>
    public Task Exited => _exited;

    public bool HasExited => _exited.IsCompleted;

    public int? ExitCode
    {
        get
        {
            if (!HasExited) return null;
            try { return _process.ExitCode; }
            catch (InvalidOperationException) { return null; }
        }
    }

    /// <summary>Havuzun/bekçinin öldürme nedeni (öldürülmediyse <c>null</c>).</summary>
    public WorkerExchangeStatus? KillStatus
    {
        get
        {
            var value = Volatile.Read(ref _killStatus);
            return value < 0 ? null : (WorkerExchangeStatus)value;
        }
    }

    /// <summary>Worker'ın stderr'e yazdığı toplam bayt (içerik değil).</summary>
    public long StderrBytes => _stderr.TotalBytes;

    /// <summary>stderr'in son ≤8KB'ı — yalnız tanı/test; LOGLANMAZ.</summary>
    internal byte[] StderrTail() => _stderr.Snapshot();

    /// <summary>
    /// Süreci başlatır ve Hello bekler. Başarısızlıkta süreç öldürülmüş, dizinler silinmiş olarak
    /// <see cref="WorkerStartException"/> fırlatılır.
    /// </summary>
    public static async Task<WorkerProcess> StartAsync(
        WorkerLaunchSpec spec,
        XsltWorkerOptions options,
        Action<int>? onLaunched,
        CancellationToken cancellationToken)
    {
        var process = new Process { StartInfo = spec.StartInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Process.Start false döndü.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            process.Dispose();
            DeleteDirectories(spec);
            throw new WorkerStartException(WorkerExitReasons.StartFailed, "Worker süreci başlatılamadı.", ex);
        }

        var worker = new WorkerProcess(process, spec, options);
        worker.TryLowerPriority();
        onLaunched?.Invoke(worker.Pid);

        try
        {
            await worker.HandshakeAsync(cancellationToken).ConfigureAwait(false);
            return worker;
        }
        catch
        {
            await worker.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

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

    /// <summary>Temiz kapatma: Shutdown çerçevesi + stdin kapat; süre dolarsa öldür.</summary>
    public async Task ShutdownAsync(string reason)
    {
        if (!HasExited)
        {
            try
            {
                await WorkerWire.WriteShutdownAsync(_stdin, new ShutdownHeader { Reason = reason }).ConfigureAwait(false);
                _stdin.Close();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Süreç zaten kapanıyor.
            }

            if (!await WaitForExitAsync(TimeSpan.FromMilliseconds(_options.ShutdownGraceMs)).ConfigureAwait(false))
                await KillAsync(WorkerExchangeStatus.Shutdown).ConfigureAwait(false);
        }
    }

    /// <summary>Süreç ağacını öldürür ve çıkışı en çok ShutdownGraceMs bekler. İlk neden korunur.</summary>
    public async Task KillAsync(WorkerExchangeStatus status)
    {
        Interlocked.CompareExchange(ref _killStatus, (int)status, -1);
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Zaten çıkmış.
        }

        await WaitForExitAsync(TimeSpan.FromMilliseconds(_options.ShutdownGraceMs)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (!HasExited)
            await KillAsync(WorkerExchangeStatus.Shutdown).ConfigureAwait(false);

        try
        {
            await _stderrPump.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Pompa kapanan boru ile kendiliğinden biter.
        }

        _process.Dispose();
        DeleteDirectories(_spec);
    }

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

    private async Task<bool> WaitForExitAsync(TimeSpan timeout) =>
        await Task.WhenAny(_exited, Task.Delay(timeout)).ConfigureAwait(false) == _exited;

    private async Task PumpStderrAsync(Stream stderr)
    {
        var buffer = new byte[4096];
        try
        {
            int read;
            while ((read = await stderr.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                _stderr.Write(buffer.AsSpan(0, read));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Süreç kapandı.
        }
    }

    private void TryLowerPriority()
    {
        if (!_options.LowerPriority)
            return;

        try
        {
            _process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // En iyi çaba.
        }
    }

    private static void Observe(Task task) =>
        task.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private static void DeleteDirectories(WorkerLaunchSpec spec)
    {
        foreach (var path in new[] { spec.WorkingDirectory, spec.TempDirectory })
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // En iyi çaba; adlar rastgele olduğundan yeniden kullanılmaz.
            }
        }
    }

    private sealed class RequestTooLargeException(string message) : Exception(message);
}
