using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace XsltCraft.Xslt.Saxon.Tests.Measurement;

/// <summary>
/// G0 Y-2 / ADR 008 "Ölçümler": template / xsl:function özyinelemesi ve derleme anı derin parantez / LRE için yığın
/// kırılma noktası (16 / 32 / 64 MB thread). Çökme süreci öldürdüğü için her ölçüm ALT SÜREÇTE
/// (<see cref="StackProbeProgram"/>) koşar; kırılma = son "OK" derinliği + sıfırdan farklı çıkış kodu.
/// CI dışı (<c>Category=Measurement</c>); sonuçlar test çıktısına ve <c>XC_MEASURE_OUT</c> (yoksa TEMP) altına yazılır.
/// Değişmez (assert): Isolated sınırı (1000) 64 MB'ta, InProcessGrid sınırı (256) 16 MB'ta kırılmanın altında.
/// </summary>
[Trait("Category", "Measurement")]
public sealed class StackBudgetMeasurementTests(ITestOutputHelper output)
{
    private static readonly int[] StacksMb = [16, 32, 64];

    [Theory]
    [InlineData("template", 250, 4_000_000)]
    [InlineData("function", 250, 4_000_000)]
    [InlineData("parens", 250, 1_500_000)]
    [InlineData("lre", 1_000, 60_000)]
    public void StackBudget_BreakingPoint(string kind, int start, int max)
    {
        var rows = new List<string>();
        var safeAt = new Dictionary<int, int>();
        foreach (var stackMb in StacksMb)
        {
            var coarse = Geometric(start, max, 1.5);
            var first = RunChild(kind, stackMb, coarse);
            var result = first;
            if (first.Crashed && first.LastOk is { } ok)
            {
                var fail = coarse.First(d => d > ok);
                var fine = Linear(ok, fail, 10);
                var refined = RunChild(kind, stackMb, fine);
                result = refined.LastOk is null
                    ? first with { FirstFail = fine[0], ExitCode = refined.ExitCode, TimedOut = refined.TimedOut }
                    : refined;
            }

            safeAt[stackMb] = result.LastOk ?? 0;
            var row = $"| {kind} | {stackMb} MB | {Fmt(result.LastOk)} | {(result.Crashed ? Fmt(result.FirstFail) : $"> {Fmt(max)} (çökmedi)")} | {result.ExitDescription} | {result.LastOkMs?.ToString(CultureInfo.InvariantCulture) ?? "-"} ms |";
            rows.Add(row);
            output.WriteLine(row);
        }

        Append("stack-budget.md", rows);

        if (kind is "template" or "function")
        {
            Assert.True(safeAt[64] > SaxonEngineOptions.Isolated.MaxTraceDepth * 2,
                $"64 MB kırılma {safeAt[64]} ≤ 2 × Isolated sınırı {SaxonEngineOptions.Isolated.MaxTraceDepth}");
            Assert.True(safeAt[16] > SaxonEngineOptions.InProcessGrid.MaxTraceDepth * 2,
                $"16 MB kırılma {safeAt[16]} ≤ 2 × grid sınırı {SaxonEngineOptions.InProcessGrid.MaxTraceDepth}");
        }
    }

    private sealed record ChildResult(int? LastOk, long? LastOkMs, int? FirstFail, int ExitCode, bool TimedOut, string Tail)
    {
        public bool Crashed => ExitCode != 0 || TimedOut;

        public string ExitDescription => TimedOut ? "zaman aşımı"
            : ExitCode switch
            {
                0 => "0",
                -1073741571 => "0xC00000FD (stack overflow)",
                -1073741819 => "0xC0000005 (yığın sınırında erişim ihlali)",
                134 => "134 (SIGABRT)",
                2 => "2 (yakalanabilir hata)",
                _ => ExitCode.ToString(CultureInfo.InvariantCulture),
            };
    }

    private ChildResult RunChild(string kind, int stackMb, IReadOnlyList<int> depths)
    {
        var dll = typeof(StackProbeProgram).Assembly.Location;
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p ? p : "dotnet";
        var psi = new ProcessStartInfo(host)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(dll)!,
        };
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add(StackProbeProgram.Flag);
        psi.ArgumentList.Add(kind);
        psi.ArgumentList.Add(stackMb.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(string.Join(',', depths));
        psi.Environment["DOTNET_DbgEnableMiniDump"] = "0";
        psi.Environment["DOTNET_EnableCrashReport"] = "0";

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stdout)
                    stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var exited = process.WaitForExit(TimeSpan.FromMinutes(3));
        if (!exited)
            process.Kill(entireProcessTree: true);
        process.WaitForExit();

        string text;
        lock (stdout)
            text = stdout.ToString();

        int? lastOk = null;
        long? lastMs = null;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ');
            if (parts[0] == "OK")
            {
                lastOk = int.Parse(parts[1], CultureInfo.InvariantCulture);
                lastMs = long.Parse(parts[2], CultureInfo.InvariantCulture);
            }
        }

        int? firstFail = lastOk is { } ok ? depths.FirstOrDefault(d => d > ok) : depths[0];
        output.WriteLine($"  {kind}/{stackMb}MB [{depths[0]}..{depths[^1]}] → exit {(exited ? process.ExitCode : -1)}, son OK {lastOk}");
        return new ChildResult(lastOk, lastMs, firstFail == 0 ? null : firstFail, exited ? process.ExitCode : -1, !exited, text.Length > 400 ? text[^400..] : text);
    }

    private static List<int> Geometric(int start, int max, double factor)
    {
        var list = new List<int>();
        for (double d = start; d <= max; d *= factor)
            list.Add((int)d);
        return list;
    }

    private static List<int> Linear(int from, int to, int steps)
    {
        var list = new List<int>();
        for (var i = 1; i <= steps; i++)
            list.Add(from + (int)((long)(to - from) * i / steps));
        return list.Distinct().ToList();
    }

    private static string Fmt(int? v) => v?.ToString("N0", CultureInfo.InvariantCulture) ?? "-";

    internal static void Append(string file, IEnumerable<string> rows)
    {
        var dir = Environment.GetEnvironmentVariable("XC_MEASURE_OUT") is { Length: > 0 } d ? d : Path.GetTempPath();
        lock (typeof(StackBudgetMeasurementTests))
            File.AppendAllLines(Path.Combine(dir, file), rows);
    }
}
