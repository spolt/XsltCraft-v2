using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Sertleştirilmiş worker başlatma bilgisi:
/// <list type="bullet">
/// <item>Ortam <c>Clear()</c> + beyaz liste — Api'nin secret'ları (connection string, JWT, OAuth, S3, AI anahtarları) ASLA geçmez.</item>
/// <item>CWD ve TMPDIR/HOME/TEMP/TMP worker'a özel, boş, tahmin edilemez adlı dizinler.</item>
/// <item>Argüman yok (dll modunda yalnız dll yolu). <c>--test-ops</c> bu sınıf tarafından ASLA eklenmez.</item>
/// </list>
/// </summary>
public sealed class WorkerProcessStartInfoFactory : IWorkerProcessStartInfoFactory
{
    /// <summary>Api'de varsa aynen geçirilen, secret içermeyen değişkenler.</summary>
    private static readonly string[] PassThrough =
    [
        "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM64",
        "LANG", "LC_ALL", "TZ", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
    ];

    /// <summary>Windows'ta çalışma zamanının ihtiyaç duyduğu sistem değişkenleri.</summary>
    private static readonly string[] WindowsPassThrough = ["SystemRoot", "windir", "SystemDrive"];

    private const string DirectoryPrefix = "xsltcraft-worker-";

    private readonly XsltWorkerOptions _options;
    private readonly XsltWorkerLocator _locator;
    private long _sequence;

    public WorkerProcessStartInfoFactory(IOptions<XsltWorkerOptions> options, XsltWorkerLocator locator)
    {
        _options = options.Value;
        _locator = locator;
    }

    public WorkerLaunchSpec Create()
    {
        var target = _locator.Locate();

        var n = Interlocked.Increment(ref _sequence);
        var name = string.Create(CultureInfo.InvariantCulture,
            $"{DirectoryPrefix}{Environment.ProcessId}-{n}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}");
        var workingDirectory = PrepareEmptyDirectory(Path.Combine(Path.GetTempPath(), name));
        var tempDirectory = PrepareEmptyDirectory(Path.Combine(Path.GetTempPath(), name + "-tmp"));

        var psi = new ProcessStartInfo(target.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in target.Arguments)
            psi.ArgumentList.Add(argument);

        BuildEnvironment(psi.Environment, tempDirectory);

        return new WorkerLaunchSpec(psi, workingDirectory, tempDirectory);
    }

    private void BuildEnvironment(IDictionary<string, string?> env, string tempDirectory)
    {
        env.Clear();

        foreach (var name in PassThrough)
            CopyIfPresent(env, name);

        if (OperatingSystem.IsWindows())
        {
            foreach (var name in WindowsPassThrough)
                CopyIfPresent(env, name);
        }
        else
        {
            env["PATH"] = "/usr/bin:/bin";
        }

        env["DOTNET_gcServer"] = "0";
        env["DOTNET_EnableDiagnostics"] = "0";
        if (_options.GcHeapHardLimitMb > 0)
        {
            // CLR yapılandırma değişkenleri onaltılık okunur.
            var bytes = (long)_options.GcHeapHardLimitMb * 1024 * 1024;
            env["DOTNET_GCHeapHardLimit"] = "0x" + bytes.ToString("X", CultureInfo.InvariantCulture);
        }

        env["TMPDIR"] = tempDirectory;
        env["TEMP"] = tempDirectory;
        env["TMP"] = tempDirectory;
        env["HOME"] = tempDirectory;
    }

    private static void CopyIfPresent(IDictionary<string, string?> env, string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value))
            env[name] = value;
    }

    private static string PrepareEmptyDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if (info.Exists || info.LinkTarget is not null)
        {
            // Rastgele ad çakışması pratikte olmaz; olursa (ör. önceden yerleştirilmiş bağlantı) izlemeden reddet.
            throw new IOException($"Worker çalışma dizini zaten var: {path}");
        }

        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(path);
        else
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return path;
    }
}
