using net.sf.saxon.lib;

namespace XsltCraft.Xslt.Saxon.Sandbox;

/// <summary>
/// Saxon'un genel günlükçüsü (varsayılanı System.err). Worker'da stdout/stderr protokol kanalıdır;
/// Api'de log'a içerik sızmamalıdır → hiçbir şey yazılmaz.
/// </summary>
internal sealed class SilentLogger : Logger
{
    public static readonly SilentLogger Instance = new();

    public override void println(string message, int severity)
    {
    }
}
