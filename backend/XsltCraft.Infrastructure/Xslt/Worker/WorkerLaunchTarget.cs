namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>Worker'ın nasıl başlatılacağı.</summary>
public enum WorkerLaunchMode
{
    /// <summary><see cref="XsltWorkerOptions.ExecutablePath"/> ile verilen yol.</summary>
    ConfiguredPath,

    /// <summary><c>{BaseDir}/xsltworker/XsltCraft.XsltWorker(.exe)</c> apphost.</summary>
    AppHost,

    /// <summary><c>dotnet {BaseDir}/xsltworker/XsltCraft.XsltWorker.dll</c>.</summary>
    DotnetDll,
}

/// <summary>Çözülmüş başlatma hedefi: çalıştırılabilir + (yalnız dll modunda) dll yolu argümanı.</summary>
public sealed record WorkerLaunchTarget(string FileName, IReadOnlyList<string> Arguments, WorkerLaunchMode Mode)
{
    /// <summary>Worker dosyalarının bulunduğu dizin.</summary>
    public required string WorkerDirectory { get; init; }
}
