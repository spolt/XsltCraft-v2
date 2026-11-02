using System.Globalization;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Komut satırı. Üretimde havuz HİÇBİR argüman geçmez. <c>--test-ops</c> yalnız test sürecinin
/// açtığı worker'larda bulunur; <c>--test-hello-protocol</c> yalnız <c>--test-ops</c> ile birlikte etkilidir.
/// Tanınmayan argüman → çıkış (sessizce yok sayılmaz).
/// </summary>
internal sealed record WorkerCommandLine(bool TestOpsEnabled, int? HelloProtocolOverride)
{
    public const string TestOpsFlag = "--test-ops";
    public const string TestHelloProtocolFlag = "--test-hello-protocol";

    public static bool TryParse(string[] args, out WorkerCommandLine commandLine)
    {
        var testOps = false;
        int? helloProtocol = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case TestOpsFlag:
                    testOps = true;
                    break;
                case TestHelloProtocolFlag when i + 1 < args.Length
                    && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value):
                    helloProtocol = value;
                    i++;
                    break;
                default:
                    commandLine = new WorkerCommandLine(false, null);
                    return false;
            }
        }

        commandLine = new WorkerCommandLine(testOps, testOps ? helloProtocol : null);
        return true;
    }
}
