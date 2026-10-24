using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.StartInfo;

public class WorkerProcessStartInfoTests
{
    /// <summary>Bağımsız beklenti listesi: üretim kodundaki listeyle eşleşmezse test kırılır.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM64",
        "LANG", "LC_ALL", "TZ", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
        "SystemRoot", "windir", "SystemDrive",
        "PATH",
        "DOTNET_gcServer", "DOTNET_EnableDiagnostics", "DOTNET_GCHeapHardLimit",
        "TMPDIR", "TEMP", "TMP", "HOME",
    };

    private static readonly string[] Secrets =
    [
        "ConnectionStrings__Default", "Jwt__Key", "Authentication__Google__ClientSecret",
        "AWS_SECRET_ACCESS_KEY", "Storage__S3__SecretKey", "Ai__Gemini__ApiKey", "XSLTCRAFT_TEST_SECRET",
    ];

    [Fact]
    public void Env_WhitelistOnly_NoSecrets()
    {
        using var secrets = new SecretEnvironment();
        var spec = WorkerTestEnvironment.ProductionFactory(WorkerTestEnvironment.Options()).Create();
        try
        {
            var env = spec.StartInfo.Environment;

            Assert.All(env.Keys, key => Assert.Contains(key, Allowed));
            Assert.All(Secrets, secret => Assert.False(env.ContainsKey(secret), $"{secret} worker'a sızdı"));
            Assert.DoesNotContain(env.Values, v => v is not null && v.Contains(SecretEnvironment.Value, StringComparison.Ordinal));

            Assert.Equal("0", env["DOTNET_gcServer"]);
            Assert.Equal("0", env["DOTNET_EnableDiagnostics"]);
            Assert.Equal("0x20000000", env["DOTNET_GCHeapHardLimit"]); // 512 MB, onaltılık
            foreach (var temp in new[] { "TMPDIR", "TEMP", "TMP", "HOME" })
                Assert.Equal(spec.TempDirectory, env[temp]);

            if (OperatingSystem.IsWindows())
                Assert.False(env.ContainsKey("PATH"));
            else
                Assert.Equal("/usr/bin:/bin", env["PATH"]);
        }
        finally
        {
            Cleanup(spec);
        }
    }

    [Fact]
    public void GcHeapHardLimit_Zero_NotPassed()
    {
        var spec = WorkerTestEnvironment.ProductionFactory(WorkerTestEnvironment.Options(o => o.GcHeapHardLimitMb = 0)).Create();
        try
        {
            Assert.False(spec.StartInfo.Environment.ContainsKey("DOTNET_GCHeapHardLimit"));
        }
        finally
        {
            Cleanup(spec);
        }
    }

    [Fact]
    public void Cwd_IsEmptyScratch()
    {
        var factory = WorkerTestEnvironment.ProductionFactory(WorkerTestEnvironment.Options());
        var a = factory.Create();
        var b = factory.Create();
        try
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            Assert.Equal(a.WorkingDirectory, a.StartInfo.WorkingDirectory);
            Assert.StartsWith(Path.Combine(tempRoot, "xsltcraft-worker-"), a.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(a.WorkingDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(a.WorkingDirectory));
            Assert.True(Directory.Exists(a.TempDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(a.TempDirectory));

            // CWD, temp dizini ve Api'nin kendi dizini birbirinden ayrı; her worker'a yeni dizin.
            Assert.NotEqual(a.WorkingDirectory, a.TempDirectory);
            Assert.NotEqual(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), a.WorkingDirectory);
            Assert.NotEqual(a.WorkingDirectory, b.WorkingDirectory);
        }
        finally
        {
            Cleanup(a);
            Cleanup(b);
        }
    }

    [Fact]
    public void TestOps_NeverPassed()
    {
        var output = AppContext.BaseDirectory;
        var appHost = Path.Combine(output, "xsltworker", "XsltCraft.XsltWorker" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var dll = Path.Combine(output, "xsltworker", "XsltCraft.XsltWorker.dll");

        var variants = new Action<XsltWorkerOptions>[]
        {
            _ => { },                                  // varsayılan konum (apphost)
            o => o.ExecutablePath = appHost,           // yapılandırılmış exe
            o => o.ExecutablePath = dll,               // dotnet <dll>
            o => { o.GcHeapHardLimitMb = 0; o.LowerPriority = false; o.PoolSize = 8; },
        };

        foreach (var configure in variants)
        {
            var options = WorkerTestEnvironment.Options(configure);
            var spec = WorkerTestEnvironment.ProductionFactory(options).Create();
            try
            {
                var args = spec.StartInfo.ArgumentList;
                Assert.DoesNotContain(args, a => a.StartsWith("--test", StringComparison.OrdinalIgnoreCase));
                Assert.True(string.IsNullOrEmpty(spec.StartInfo.Arguments));
                if (options.ExecutablePath?.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true)
                    Assert.Equal([Path.GetFullPath(dll)], args);
                else
                    Assert.Empty(args);
            }
            finally
            {
                Cleanup(spec);
            }
        }
    }

    [Fact]
    public void Locator_DefaultFindsAppHost()
    {
        var target = new XsltWorkerLocator(Microsoft.Extensions.Options.Options.Create(new XsltWorkerOptions())).Locate();

        Assert.Equal(WorkerLaunchMode.AppHost, target.Mode);
        Assert.Empty(target.Arguments);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "xsltworker"), target.WorkerDirectory);
        Assert.True(File.Exists(target.FileName));
    }

    [Fact]
    public void Locator_MissingConfiguredPath_Throws()
    {
        var options = new XsltWorkerOptions { ExecutablePath = Path.Combine(Path.GetTempPath(), "no-such-worker-" + Guid.NewGuid().ToString("N")) };
        Assert.Throws<FileNotFoundException>(() => new XsltWorkerLocator(Microsoft.Extensions.Options.Options.Create(options)).Locate());
    }

    [Fact]
    [Trait("Category", "WorkerIntegration")]
    public async Task Env_WorkerProcessSeesOnlyWhitelist()
    {
        using var secrets = new SecretEnvironment();
        await using var h = await PoolHarness.StartAsync();

        var report = await h.RunAsync(TestOpNames.EnvReport);
        Assert.True(report.Ok);
        var lines = report.Response.Output!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var names = lines.Where(l => l.StartsWith("env=", StringComparison.Ordinal)).Select(l => l[4..]).ToList();
        Assert.All(names, name => Assert.Contains(name, Allowed));
        Assert.All(Secrets, secret => Assert.DoesNotContain(secret, names));

        var cwd = lines.Single(l => l.StartsWith("cwd=", StringComparison.Ordinal))[4..];
        Assert.Contains("xsltcraft-worker-", cwd, StringComparison.OrdinalIgnoreCase);
        var temp = lines.Single(l => l.StartsWith("temp=", StringComparison.Ordinal))[5..];
        Assert.Contains("xsltcraft-worker-", temp, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("-tmp", temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [Fact]
    [Trait("Category", "WorkerIntegration")]
    public async Task GcHeapHardLimit_AppliedInWorker()
    {
        await using var h = await PoolHarness.StartAsync(o => o.GcHeapHardLimitMb = 256);

        var info = await h.RunAsync(TestOpNames.GcInfo);
        Assert.True(info.Ok);

        var parts = info.Response.Output!.Split(';').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        var available = long.Parse(parts["totalAvailable"], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(available <= 256L * 1024 * 1024, $"GC tavanı uygulanmadı: {available}");
        Assert.Equal("False", parts["server"]);
    }

    private static void Cleanup(WorkerLaunchSpec spec)
    {
        foreach (var dir in new[] { spec.WorkingDirectory, spec.TempDirectory })
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Test süresince Api sürecine sahte secret'lar koyar.</summary>
    private sealed class SecretEnvironment : IDisposable
    {
        public const string Value = "s3cr3t-value-must-not-leak";

        public SecretEnvironment()
        {
            foreach (var name in Secrets)
                Environment.SetEnvironmentVariable(name, Value);
        }

        public void Dispose()
        {
            foreach (var name in Secrets)
                Environment.SetEnvironmentVariable(name, null);
        }
    }
}
