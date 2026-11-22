using System.Collections.Concurrent;

namespace XsltCraft.Api.Downloads;

/// <summary>
/// ZIP indirmeleri için eşzamanlılık kapısı ve geçici dosya deposu (singleton).
/// Kullanıcı başına aynı anda tek indirme, sunucu genelinde en fazla <see cref="MaxConcurrentArchives"/> arşiv.
/// İzin (lease) yanıt tamamen gönderilene kadar tutulur (<c>Response.RegisterForDispose</c>), böylece diskteki
/// geçici arşiv sayısı sınırlı kalır. Geçici dosyalar uygulamaya özel dizinde (Unix'te 0700) tutulur;
/// süreç çökmesinden kalan eski dosyalar ilk kullanımda temizlenir.
/// </summary>
public sealed class ArchiveDownloadGate
{
    public const int MaxConcurrentArchives = 3;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _global = new(MaxConcurrentArchives, MaxConcurrentArchives);
    private readonly ConcurrentDictionary<Guid, byte> _activeUsers = new();
    private readonly string _directory;
    private int _cleaned;

    public ArchiveDownloadGate() : this(Path.Combine(Path.GetTempPath(), "xsltcraft-zip")) { }

    public ArchiveDownloadGate(string directory) => _directory = directory;

    public enum Denial { None, UserBusy, ServerBusy }

    /// <summary>İzin alınamazsa <paramref name="lease"/> null ve sebep döner; kuyruk yoktur.</summary>
    public Denial TryEnter(Guid userId, out IDisposable? lease)
    {
        lease = null;
        if (!_activeUsers.TryAdd(userId, 0))
            return Denial.UserBusy;
        if (!_global.Wait(0))
        {
            _activeUsers.TryRemove(userId, out _);
            return Denial.ServerBusy;
        }
        lease = new Lease(this, userId);
        return Denial.None;
    }

    /// <summary>Yanıt bitince silinen geçici arşiv dosyası açar.</summary>
    public FileStream CreateTempFile()
    {
        EnsureDirectory();
        return new FileStream(
            Path.Combine(_directory, $"{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    private void EnsureDirectory()
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(_directory);
        else
            Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        if (Interlocked.Exchange(ref _cleaned, 1) == 0)
            DeleteStaleFiles();
    }

    private void DeleteStaleFiles()
    {
        var cutoff = DateTime.UtcNow - StaleAfter;
        foreach (var file in Directory.EnumerateFiles(_directory, "*.tmp"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class Lease(ArchiveDownloadGate gate, Guid userId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            gate._activeUsers.TryRemove(userId, out _);
            gate._global.Release();
        }
    }
}
