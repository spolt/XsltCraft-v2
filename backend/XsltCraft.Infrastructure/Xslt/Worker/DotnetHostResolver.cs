namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// <c>dotnet</c> host'unun mutlak yolu. Worker ortamı temizlendiği (PATH yok/kısıtlı) için
/// yol Api sürecinde, mutlak olarak çözülür.
/// </summary>
internal static class DotnetHostResolver
{
    private static readonly string ExeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

    public static string? Resolve()
    {
        // 1) dotnet CLI'nin alt süreçlere verdiği yol (dotnet run/test).
        if (IsFile(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")) is { } hostPath)
            return hostPath;

        // 2) Api'nin kendisi 'dotnet Api.dll' ile çalışıyorsa.
        var processPath = Environment.ProcessPath;
        if (processPath is not null && string.Equals(Path.GetFileName(processPath), ExeName, StringComparison.OrdinalIgnoreCase))
            return processPath;

        // 3) DOTNET_ROOT.
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root
            && IsFile(Path.Combine(root, ExeName)) is { } fromRoot)
            return fromRoot;

        // 4) PATH.
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsFile(Path.Combine(dir, ExeName)) is { } fromPath)
                return fromPath;
        }

        // 5) Varsayılan kurulum yerleri.
        string[] defaults = OperatingSystem.IsWindows()
            ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", ExeName)]
            : ["/usr/share/dotnet/dotnet", "/usr/lib/dotnet/dotnet", "/usr/local/share/dotnet/dotnet"];
        return defaults.Select(IsFile).FirstOrDefault(p => p is not null);
    }

    private static string? IsFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : null;
}
