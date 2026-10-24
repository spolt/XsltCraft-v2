using System.Reflection;
using System.Runtime.InteropServices;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// ADR 008 açık soru 5: IKVM ile dönüşmüş Saxon derlemesi ve <c>ikvm/&lt;rid&gt;</c> "Java home"u, yalnız
/// Xslt.Saxon'da bildirilen IKVM/Maven bağımlılığına rağmen ProjectReference zinciriyle worker (1 seviye) ve Api
/// (Api → Infrastructure → Xslt.Saxon, 2 seviye; süreç içi grid host'u) çıktılarına iniyor mu? Api/Infrastructure'da
/// IKVM PackageReference YOK (LayeringTests) — zincir yeterli olmalı.
/// </summary>
public sealed class IkvmFlowTests
{
    [Fact]
    public void WorkerOutput_ContainsSaxonAndIkvmHome()
    {
        var workerDir = OutputDir("XsltWorkerTargetPath");

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
    public void ApiOutput_ContainsSaxonAndIkvmHome()
    {
        var apiDir = OutputDir("XsltApiTargetPath");

        Assert.True(File.Exists(Path.Combine(apiDir, "XsltCraft.Api.dll")), apiDir);
        Assert.True(File.Exists(Path.Combine(apiDir, "XsltCraft.Infrastructure.dll")));
        Assert.True(File.Exists(Path.Combine(apiDir, "XsltCraft.Xslt.Saxon.dll")));
        Assert.True(File.Exists(Path.Combine(apiDir, "Saxon.HE.dll")));
        Assert.True(File.Exists(Path.Combine(apiDir, "org.xmlresolver.xmlresolver.dll")));
        Assert.True(File.Exists(Path.Combine(apiDir, "org.xmlresolver.xmlresolver_data.dll")));
        Assert.True(HasRuntimeAsset(apiDir, "IKVM.Runtime.dll"), "IKVM.Runtime.dll yok");
        Assert.True(HasRuntimeAsset(apiDir, "IKVM.Java.dll"), "IKVM.Java.dll yok");
        Assert.True(File.Exists(Path.Combine(apiDir, "ikvm.properties")));

        var ikvmHome = Path.Combine(apiDir, "ikvm", RuntimeInformation.RuntimeIdentifier);
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

    private static string OutputDir(string metadataKey)
    {
        var targetPath = typeof(IkvmFlowTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == metadataKey).Value;
        Assert.False(string.IsNullOrWhiteSpace(targetPath));
        return Path.GetDirectoryName(targetPath)!;
    }
}
