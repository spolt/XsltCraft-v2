using System.Runtime.CompilerServices;
using System.Text;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Yalnız <c>--test-ops</c> ile açılan arıza enjeksiyonu op'ları (havuz bu bayrağı ASLA geçmez).
/// Gerçek süreç davranışlarını (yakalanamaz StackOverflow, bellek şişmesi, fd 1 kirliliği, stderr selesi)
/// Api tarafı havuz testleri için üretir.
/// </summary>
internal sealed class TestOps
{
    public const string CrashStackOverflow = "crash-stackoverflow";
    public const string Sleep = "sleep";
    public const string Alloc = "alloc";
    public const string StdoutGarbage = "stdout-garbage";
    public const string StderrFlood = "stderr-flood";
    public const string ConsoleWrite = "console-write";
    public const string RequestRecycle = "request-recycle";
    public const string GcInfo = "gc-info";
    public const string EnvReport = "env-report";

    /// <summary>render, ama XsltSafety taraması ATLANIR (RequestHandler yönlendirir; T2.3 izolasyon testleri).</summary>
    public const string RenderUnscreened = "render-unscreened";

    /// <summary>Java <c>System.out</c>/<c>System.err</c>'e doğrudan yazar (JavaStdio yönlendirmesinin kanıtı).</summary>
    public const string JavaStdout = "java-stdout";

    /// <summary>İşin koştuğu thread'in adı (motor thread'i doğrulaması).</summary>
    public const string ThreadInfo = "thread-info";

    private const int Megabyte = 1024 * 1024;

    // alloc op'unun tuttuğu bellek: GC toplayamasın diye statik kökte.
    private static readonly List<byte[]> Retained = [];

    public bool TryHandle(RequestHeader request, IReadOnlyList<byte[]> bodies, out HandlerResult result)
    {
        result = request.Op switch
        {
            CrashStackOverflow => Crash(),
            Sleep => DoSleep(request),
            Alloc => DoAlloc(request),
            StdoutGarbage => WriteStdoutGarbage(request),
            StderrFlood => FloodStderr(request),
            ConsoleWrite => WriteConsole(request),
            RequestRecycle => RecycleRequested(request),
            GcInfo => Text(request, $"totalAvailable={GC.GetGCMemoryInfo().TotalAvailableMemoryBytes};server={System.Runtime.GCSettings.IsServerGC}"),
            EnvReport => Text(request, BuildEnvReport()),
            JavaStdout => WriteJavaStdio(request),
            ThreadInfo => Text(request, $"name={Thread.CurrentThread.Name}"),
            _ => null!,
        };
        return result is not null;
    }

    private static HandlerResult Crash()
    {
        Recurse(0);
        throw new InvalidOperationException("ulaşılamaz");
    }

    // Kuyruk çağrısı olmasın diye özyinelemeden SONRA iş yapılır; stackalloc her çerçeveyi büyütür.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Recurse(long depth)
    {
        Span<byte> pad = stackalloc byte[256];
        pad[(int)(depth & 0xFF)] = 1;
        return Recurse(depth + 1) + pad[0];
    }

    private static HandlerResult DoSleep(RequestHeader request)
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(request.TestArgument ?? 1000));
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult DoAlloc(RequestHeader request)
    {
        var megabytes = request.TestArgument ?? 256;
        try
        {
            for (var i = 0; i < megabytes; i++)
            {
                var chunk = new byte[Megabyte];
                for (var p = 0; p < chunk.Length; p += 4096)
                    chunk[p] = 1; // sayfaları gerçekten işle → WorkingSet artsın
                Retained.Add(chunk);
            }
        }
        catch (OutOfMemoryException)
        {
            // GC tavanı (DOTNET_GCHeapHardLimit) aşıldı. Gerçek bir dönüşümde olduğu gibi isteğin ayırdığı bellek
            // istisnayla birlikte erişilemez olur; RequestHandler MemoryLimit + recycle yanıtı kurabilsin.
            Retained.Clear();
            throw;
        }

        // Belleği tutarak bekle: Api bellek bekçisi bu arada öldürmeli.
        Thread.Sleep(TimeSpan.FromSeconds(10));
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult WriteStdoutGarbage(RequestHeader request)
    {
        // Yerel kodun fd 1'e doğrudan yazmasını taklit eder (Console.Out korumasını atlar).
        using var raw = Console.OpenStandardOutput();
        raw.Write("garbage: not a protocol frame\n"u8);
        raw.Flush();
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult FloodStderr(RequestHeader request)
    {
        var megabytes = request.TestArgument ?? 8;
        var chunk = new byte[64 * 1024];
        Array.Fill(chunk, (byte)'E');
        using var stderr = Console.OpenStandardError();
        for (long written = 0; written < megabytes * Megabyte; written += chunk.Length)
            stderr.Write(chunk);
        stderr.Flush();
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult WriteJavaStdio(RequestHeader request)
    {
        // JavaStdio olmadan IKVM System.out fd 1'e yazar → Api protokol ihlali sayıp worker'ı öldürürdü.
        java.lang.System.@out.println("java stdout line must never reach the protocol stream");
        java.lang.System.@out.flush();
        java.lang.System.err.println("java stderr line (bounded)");
        java.lang.System.err.flush();
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult WriteConsole(RequestHeader request)
    {
        // StdioGuard'dan sonra Console.Out → TextWriter.Null: protokol bozulmamalı.
        Console.WriteLine("this line must never reach the protocol stream");
        Console.Out.Flush();
        return RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
    }

    private static HandlerResult RecycleRequested(RequestHeader request)
    {
        var ok = RequestHandler.Ok(request.Id, HandlerResult.NoBodies);
        return ok with { Header = ok.Header with { RecycleRequested = true } };
    }

    private static string BuildEnvReport()
    {
        var sb = new StringBuilder();
        sb.Append("cwd=").Append(Environment.CurrentDirectory).Append('\n');
        sb.Append("temp=").Append(Path.GetTempPath()).Append('\n');
        var names = Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal);
        foreach (var name in names)
            sb.Append("env=").Append(name).Append('\n');
        return sb.ToString();
    }

    private static HandlerResult Text(RequestHeader request, string text) =>
        RequestHandler.Ok(request.Id, [WorkerWire.EncodeBody(text)]);
}
