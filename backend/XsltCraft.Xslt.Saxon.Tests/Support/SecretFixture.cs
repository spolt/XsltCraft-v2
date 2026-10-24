using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XsltCraft.Xslt.Saxon.Tests.Support;

/// <summary>
/// Gerçek erişim kanıtı için geçici "secret" dosyalar + iki loopback dinleyici:
/// <list type="bullet">
/// <item><see cref="HttpUri"/>: bağlantıyı kabul edip kapatan "sessiz" TCP dinleyici (yalnız sayaç).</item>
/// <item><see cref="LiveHttpUri"/>: GERÇEK bir HTTP 200 yanıtı (gövdede <see cref="Marker"/>) dönen dinleyici —
///   sandbox delinirse işaretçi çıktıya düşer; "bağlantı kuruldu ama içerik okunamadı" ile karıştırılmaz.</item>
/// </list>
/// Sandbox delinirse ya <see cref="Marker"/> çıktıda görünür ya da <see cref="Connections"/> / <see cref="LiveConnections"/> artar.
/// </summary>
public sealed class SecretFixture : IDisposable
{
    public const string Marker = "GIZLI_DOSYA_ICERIGI_7f3a";
    public const string EnvName = "XSLTCRAFT_TEST_SECRET_ENV";

    private readonly TcpListener _listener;
    private readonly TcpListener _live;
    private int _connections;
    private int _liveConnections;

    public SecretFixture()
    {
        Dir = Directory.CreateTempSubdirectory("xc-saxon-k3").FullName;
        SecretXml = Path.Combine(Dir, "secret.xml");
        SecretTxt = Path.Combine(Dir, "secret.txt");
        SecretXsl = Path.Combine(Dir, "secret.xsl");
        SecretJson = Path.Combine(Dir, "secret.json");
        SecretXq = Path.Combine(Dir, "secret.xq");
        SecretXsd = Path.Combine(Dir, "secret.xsd");
        File.WriteAllText(SecretXml, $"<s>{Marker}</s>");
        File.WriteAllText(SecretTxt, Marker);
        File.WriteAllText(SecretJson, "{\"k\":\"" + Marker + "\"}");
        File.WriteAllText(SecretXq, $"module namespace m = 'urn:m'; declare function m:f() {{ '{Marker}' }};");
        File.WriteAllText(SecretXsd, $"<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" targetNamespace=\"urn:s\"><xs:element name=\"{Marker}\"/></xs:schema>");
        File.WriteAllText(SecretXsl, $"""
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template name="leak"><xsl:text>{Marker}</xsl:text></xsl:template>
              <xsl:template match="/"><o>{Marker}</o></xsl:template>
            </xsl:stylesheet>
            """);

        // Test sürecinin CWD'sine "appsettings.json benzeri" secret (worker/Api'de CWD → /app/appsettings.json riski).
        CwdSecretName = $"appsettings.xc-k3-{Guid.NewGuid():N}.json";
        CwdSecretXmlName = $"appsettings.xc-k3-{Guid.NewGuid():N}.xml";
        CwdSecretXslName = $"appsettings.xc-k3-{Guid.NewGuid():N}.xsl";
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, CwdSecretName), "{\"ConnectionStrings\":{\"Db\":\"" + Marker + "\"}}");
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, CwdSecretXmlName), $"<s>{Marker}</s>");
        File.Copy(SecretXsl, Path.Combine(Environment.CurrentDirectory, CwdSecretXslName));

        Environment.SetEnvironmentVariable(EnvName, Marker);

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _ = AcceptLoop();

        _live = new TcpListener(IPAddress.Loopback, 0);
        _live.Start();
        _ = LiveLoop();
    }

    public string Dir { get; }
    public string SecretXml { get; }
    public string SecretTxt { get; }
    public string SecretXsl { get; }
    public string SecretJson { get; }
    public string SecretXq { get; }
    public string SecretXsd { get; }

    /// <summary>CWD'deki secret dosya adları (relative URI ile denenecek).</summary>
    public string CwdSecretName { get; }
    public string CwdSecretXmlName { get; }
    public string CwdSecretXslName { get; }

    public int Connections => Volatile.Read(ref _connections);
    public int LiveConnections => Volatile.Read(ref _liveConnections);
    public int TotalConnections => Connections + LiveConnections;
    public string ResultDocPath => Path.Combine(Dir, "result-doc-out.xml");

    public static string FileUri(string path) => new Uri(path).AbsoluteUri;

    public static string CwdFileUri => new Uri(Path.Combine(Environment.CurrentDirectory, "x")).AbsoluteUri;

    public string HttpUri(string path) => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{path}";

    public string LiveHttpUri(string path) => $"http://127.0.0.1:{((IPEndPoint)_live.LocalEndpoint).Port}/{path}";

    /// <summary>Canlı dinleyicinin gerçekten yanıt verdiğini (kontrol) .NET istemcisiyle doğrular.</summary>
    public async Task<string> FetchLiveAsync(string path)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(LiveHttpUri(path));
    }

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

    private async Task LiveLoop()
    {
        var body = Encoding.UTF8.GetBytes($"<s>{Marker}</s>");
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/xml\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        try
        {
            while (true)
            {
                using var c = await _live.AcceptTcpClientAsync();
                Interlocked.Increment(ref _liveConnections);
                try
                {
                    var stream = c.GetStream();
                    var buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                    await stream.WriteAsync(head);
                    await stream.WriteAsync(body);
                }
                catch (Exception ex) when (ex is IOException or TimeoutException or SocketException)
                {
                }
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
        _live.Stop();
        Environment.SetEnvironmentVariable(EnvName, null);
        foreach (var name in new[] { CwdSecretName, CwdSecretXmlName, CwdSecretXslName })
        {
            try
            {
                File.Delete(Path.Combine(Environment.CurrentDirectory, name));
            }
            catch (IOException)
            {
            }
        }

        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

[CollectionDefinition(nameof(SecretFixtureCollection))]
public sealed class SecretFixtureCollection : ICollectionFixture<SecretFixture>
{
}
