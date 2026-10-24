using System.Runtime.InteropServices;

using Microsoft.Extensions.Configuration;

namespace XsltCraft.Infrastructure.Security;

/// <summary>
/// Api süreci açılış sertleştirmesi. Linux'ta <c>prctl(PR_SET_DUMPABLE, 0)</c>: çekirdek dökümü kapanır ve
/// aynı uid'deki süreçler (ör. ele geçirilmiş bir alt süreç) <c>ptrace</c> / <c>/proc/&lt;pid&gt;/mem</c> ile
/// Api belleğini (JWT anahtarı, DB parolası) okuyamaz. Ayar: <c>Security:NonDumpable</c>
/// (Production true; Development false — hata ayıklayıcı bağlanabilsin). Windows/macOS'ta no-op.
/// </summary>
public static class ProcessHardening
{
    public const string NonDumpableKey = "Security:NonDumpable";

    private const int PrSetDumpable = 4;

    /// <summary>Ayar açıksa uygular; başarıyı döner (kapalı/desteklenmeyen platform → <c>false</c>).</summary>
    public static bool Apply(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>(NonDumpableKey) || !OperatingSystem.IsLinux())
            return false;

        if (prctl(PrSetDumpable, 0, 0, 0, 0) == 0)
            return true;

        // Açılışı durdurmaz: konteyner seccomp profili prctl'i engelliyorsa yalnız uyar.
        Console.Error.WriteLine($"[ProcessHardening] prctl(PR_SET_DUMPABLE, 0) başarısız (errno {Marshal.GetLastPInvokeError()}).");
        return false;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);
}
