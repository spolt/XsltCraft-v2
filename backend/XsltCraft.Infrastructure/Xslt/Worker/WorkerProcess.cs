using System.ComponentModel;
using System.Diagnostics;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Tek bir worker süreci: başlatma + Hello denetimi, aynı anda TEK istek, stderr'in sınırlı halka
/// tampona boşaltılması, WorkingSet bekçisi ve sert zaman aşımı. Her arızada (zaman aşımı, bellek,
/// protokol ihlali) süreç ağacı öldürülür — worker içindeki yakalanamaz hatalar (StackOverflow) Api'yi
/// etkilemez. Thread-safe DEĞİL: havuz bir worker'a aynı anda tek istek verir.
/// </summary>
internal sealed partial class WorkerProcess : IAsyncDisposable
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

    private WorkerProcess(Process process, WorkerLaunchSpec spec, XsltWorkerOptions options, TimeProvider timeProvider)
    {
        _process = process;
        _spec = spec;
        _options = options;
        Pid = process.Id;
        StartedAt = timeProvider.GetUtcNow();
        _stdin = process.StandardInput.BaseStream;
        _stdout = process.StandardOutput.BaseStream;
        _exited = process.WaitForExitAsync();
        _stderrPump = Task.Run(() => PumpStderrAsync(process.StandardError.BaseStream));
    }

    public int Pid { get; }

    /// <summary>Başlatma anı — havuzun <see cref="TimeProvider"/>'ından (yaş tabanlı geri dönüşüm aynı saatle ölçülür).</summary>
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
        TimeProvider timeProvider,
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

        WorkerProcess worker;
        try
        {
            worker = new WorkerProcess(process, spec, options, timeProvider);
        }
        catch (Exception ex)
        {
            // Süreç başladı ama sarmalanamadı: yetim bırakma.
            KillAndDispose(process);
            DeleteDirectories(spec);
            throw new WorkerStartException(WorkerExitReasons.StartFailed, $"Worker süreci sarmalanamadı ({ex.GetType().Name}).", ex);
        }

        try
        {
            worker.TryLowerPriority();
            onLaunched?.Invoke(worker.Pid);
            await worker.HandshakeAsync(cancellationToken).ConfigureAwait(false);
            return worker;
        }
        catch
        {
            await worker.DisposeAsync().ConfigureAwait(false);
            throw;
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

    private static void KillAndDispose(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Zaten çıkmış.
        }

        process.Dispose();
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
}
