using Microsoft.Extensions.Options;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// Worker çalıştırılabilirini bulur. Sıra:
/// <list type="number">
/// <item><see cref="XsltWorkerOptions.ExecutablePath"/> (verildiyse; yoksa hata — sessizce düşmez),</item>
/// <item><c>{AppContext.BaseDirectory}/xsltworker/XsltCraft.XsltWorker(.exe)</c> apphost,</item>
/// <item><c>dotnet {…}/xsltworker/XsltCraft.XsltWorker.dll</c>.</item>
/// </list>
/// Her başlatmada yeniden çözülür (ucuz dosya denetimi; dağıtım sonrası yol değişimine dayanıklı).
/// </summary>
public sealed class XsltWorkerLocator
{
    public const string WorkerDirectoryName = "xsltworker";
    public const string WorkerAssemblyName = "XsltCraft.XsltWorker";

    private readonly XsltWorkerOptions _options;
    private readonly string _baseDirectory;
    private readonly Func<string?> _resolveDotnetHost;

    public XsltWorkerLocator(IOptions<XsltWorkerOptions> options)
        : this(options.Value, AppContext.BaseDirectory, DotnetHostResolver.Resolve)
    {
    }

    internal XsltWorkerLocator(XsltWorkerOptions options, string baseDirectory, Func<string?> resolveDotnetHost)
    {
        _options = options;
        _baseDirectory = baseDirectory;
        _resolveDotnetHost = resolveDotnetHost;
    }

    public WorkerLaunchTarget Locate()
    {
        if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
            return FromConfiguredPath(Path.GetFullPath(_options.ExecutablePath));

        var workerDirectory = Path.Combine(_baseDirectory, WorkerDirectoryName);

        var appHost = Path.Combine(workerDirectory, WorkerAssemblyName + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (File.Exists(appHost))
            return new WorkerLaunchTarget(appHost, [], WorkerLaunchMode.AppHost) { WorkerDirectory = workerDirectory };

        var dll = Path.Combine(workerDirectory, WorkerAssemblyName + ".dll");
        if (File.Exists(dll))
            return ViaDotnet(dll, WorkerLaunchMode.DotnetDll);

        throw new FileNotFoundException(
            $"XSLT worker bulunamadı. Denenen: {appHost}, {dll}. '{XsltWorkerOptions.SectionName}:{nameof(XsltWorkerOptions.ExecutablePath)}' ile yol verilebilir.");
    }

    private WorkerLaunchTarget FromConfiguredPath(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Yapılandırılmış XSLT worker yolu bulunamadı: {path}");

        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return ViaDotnet(path, WorkerLaunchMode.ConfiguredPath);

        return new WorkerLaunchTarget(path, [], WorkerLaunchMode.ConfiguredPath)
        {
            WorkerDirectory = Path.GetDirectoryName(path)!,
        };
    }

    private WorkerLaunchTarget ViaDotnet(string dll, WorkerLaunchMode mode)
    {
        var dotnet = _resolveDotnetHost()
            ?? throw new FileNotFoundException($"'dotnet' bulunamadı; worker dll'i çalıştırılamıyor: {dll}");
        return new WorkerLaunchTarget(dotnet, [dll], mode) { WorkerDirectory = Path.GetDirectoryName(dll)! };
    }
}
