using XsltCraft.XsltWorker.Hosting;

// İzole XSLT worker'ı. Host builder YOK, appsettings okunmaz: tüm girdi stdin çerçevelerinden gelir.
// Sıra önemli: stdout protokol akışı, herhangi bir kod Console.Out'a yazamadan önce ele geçirilir.

if (!WorkerCommandLine.TryParse(args, out var commandLine))
    return WorkerExitCodes.BadArguments;

ProcessHardening.Apply();

var stdio = StdioGuard.Acquire();
var handler = new RequestHandler(commandLine.TestOpsEnabled ? new TestOps() : null);
var host = new WorkerHost(stdio.Input, stdio.Output, handler, commandLine);

return await host.RunAsync();
