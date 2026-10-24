using System.Reflection;
using System.Runtime.InteropServices;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// ADR 008 açık soru 5: IKVM ile dönüşmüş Saxon derlemesi ve <c>ikvm/&lt;rid&gt;</c> "Java home"u, yalnız
/// Xslt.Saxon'da bildirilen IKVM/Maven bağımlılığına rağmen ProjectReference zinciriyle worker çıktısına iniyor mu?
/// (Api tarafı T2.4b'de: <c>ApiOutput_ContainsSaxonAndIkvmHome</c>.)
/// </summary>
public sealed class IkvmFlowTests
{
    [Fact]
    public void WorkerOutput_ContainsSaxonAndIkvmHome()
    {
        var workerDir = WorkerOutputDir();

        Assert.True(File.Exists(Path.Combine(workerDir, "XsltCraft.XsltWorker.dll")), workerDir);
        Assert.True(File.Exists(Path.Combine(workerDir, "XsltCraft.Xslt.Saxon.dll")));
        Assert.True(File.Exists(Path.Combine(workerDir, "Saxon.HE.dll")));
        Assert.True(File.Exists(Path.Combine(workerDir, "org.xmlresolver.xmlresolver.dll")));
        Assert.True(File.Exists(Path.Combine(workerDir, "org.xmlresolver.xmlresolver_data.dll")));
        Assert.True(HasRuntimeAsset(workerDir, "IKVM.Runtime.dll"), "IKVM.Runtime.dll yok");
        Assert.True(HasRuntimeAsset(workerDir, "IKVM.Java.dll"), "IKVM.Java.dll yok");
        Assert.True(File.Exists(Path.Combine(workerDir, "ikvm.properties")));

        var rid = RuntimeInformation.RuntimeIdentifier;
        var ikvmHome = Path.Combine(workerDir, "ikvm", rid);
        Assert.True(Directory.Exists(ikvmHome), $"IKVM home yok: {ikvmHome}");
        Assert.True(Directory.Exists(Path.Combine(ikvmHome, "lib")));
    }

    [Fact]
    public void TestOutput_ContainsSaxonAndIkvmHome()
    {
        // Bu test derlemesi de Saxon'u aynı yoldan alır (IKVM yeniden dönüştürülmez, önbellekten gelir).
        var dir = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(dir, "Saxon.HE.dll")));
        Assert.True(Directory.Exists(Path.Combine(dir, "ikvm", RuntimeInformation.RuntimeIdentifier)));
    }

    // RID'siz build: runtimes/<os>/lib/<tfm>/; RID'li publish (-r linux-x64): çıktı kökü.
    private static bool HasRuntimeAsset(string dir, string file)
    {
        if (File.Exists(Path.Combine(dir, file)))
            return true;

        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var libDir = Path.Combine(dir, "runtimes", os, "lib");
        return Directory.Exists(libDir) && Directory.EnumerateFiles(libDir, file, SearchOption.AllDirectories).Any();
    }

    private static string WorkerOutputDir()
    {
        var targetPath = typeof(IkvmFlowTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "XsltWorkerTargetPath").Value;
        Assert.False(string.IsNullOrWhiteSpace(targetPath));
        return Path.GetDirectoryName(targetPath)!;
    }
}
