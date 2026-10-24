using System.Net;
using System.Net.Sockets;

namespace XsltCraft.Xslt.Saxon.Tests.Support;

/// <summary>
/// Gerçek erişim kanıtı için geçici "secret" dosyalar + loopback TCP dinleyici. Sandbox delinirse ya
/// <see cref="Marker"/> çıktıda görünür ya da <see cref="Connections"/> artar.
/// </summary>
public sealed class SecretFixture : IDisposable
{
    public const string Marker = "GIZLI_DOSYA_ICERIGI_7f3a";
    public const string EnvName = "XSLTCRAFT_TEST_SECRET_ENV";

    private readonly TcpListener _listener;
    private int _connections;

    public SecretFixture()
    {
        Dir = Directory.CreateTempSubdirectory("xc-saxon-k3").FullName;
        SecretXml = Path.Combine(Dir, "secret.xml");
        SecretTxt = Path.Combine(Dir, "secret.txt");
        SecretXsl = Path.Combine(Dir, "secret.xsl");
        File.WriteAllText(SecretXml, $"<s>{Marker}</s>");
        File.WriteAllText(SecretTxt, Marker);
        File.WriteAllText(SecretXsl, $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template name="leak"><xsl:text>{Marker}</xsl:text></xsl:template>
              <xsl:template match="/"><o>{Marker}</o></xsl:template>
            </xsl:stylesheet>
            """);
        Environment.SetEnvironmentVariable(EnvName, Marker);

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _ = AcceptLoop();
    }

    public string Dir { get; }
    public string SecretXml { get; }
    public string SecretTxt { get; }
    public string SecretXsl { get; }
    public int Connections => Volatile.Read(ref _connections);
    public string ResultDocPath => Path.Combine(Dir, "result-doc-out.xml");

    public static string FileUri(string path) => new Uri(path).AbsoluteUri;

    public string HttpUri(string path) => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{path}";

    private async Task AcceptLoop()
    {
        try
        {
            while (true)
            {
                using var c = await _listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref _connections);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        Environment.SetEnvironmentVariable(EnvName, null);
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
