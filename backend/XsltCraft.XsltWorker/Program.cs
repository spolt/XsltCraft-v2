using XsltCraft.XsltWorker.Hosting;

// İzole XSLT worker'ı. Host builder YOK, appsettings okunmaz: tüm girdi stdin çerçevelerinden gelir.
// Sıra önemli:
//  1. stdout protokol akışı, herhangi bir kod Console.Out'a yazamadan önce ele geçirilir (StdioGuard).
//  2. Motor thread'i (64 MB yığın) açılır; Java akışları yönlendirilir (JavaStdio — IKVM System.out fd 1'e yazar),
//     motorlar kurulur ve ısıtılır. Hepsi Hello'dan ÖNCE: havuz Hello'yu Ready sayar → Ready = ısınmış worker.
//  3. Isınma başarısızsa Hello gönderilmez, WarmupFailed ile çıkılır (havuz başlatma hatası sayar, backoff uygular).

if (!WorkerCommandLine.TryParse(args, out var commandLine))
    return WorkerExitCodes.BadArguments;

ProcessHardening.Apply();

var stdio = StdioGuard.Acquire();
using var engineThread = new EngineThread();

WorkerEngineSet engines;
try
{
    var stderr = Console.OpenStandardError();
    engines = engineThread.Run(() => EngineWarmup.CreateAndWarm(stderr));
}
catch (Exception)
{
    // Ayrıntı yazılmaz (stderr içeriği loglanmaz; çıkış kodu yeterli).
    return WorkerExitCodes.WarmupFailed;
}

var handler = new RequestHandler(engines, new CompileCache(), commandLine.TestOpsEnabled ? new TestOps() : null);
var host = new WorkerHost(stdio.Input, stdio.Output, handler, engineThread, commandLine, engines.SaxonVersion);

return await host.RunAsync();
