using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// <c>environment-variable()</c> / <c>available-environment-variables()</c> hiçbir şey görmez.
/// Varsayılan çözümleyici süreç ortamının TAMAMINI verir (Api sürecinde secret'lar!).
/// </summary>
internal sealed class EmptyEnvironmentVariableResolver : EnvironmentVariableResolver
{
    public static readonly EmptyEnvironmentVariableResolver Instance = new();

    public java.util.Set getAvailableEnvironmentVariables() => new java.util.HashSet();

    public string? getEnvironmentVariable(string name) => null;
}
