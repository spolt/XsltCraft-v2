using System.Diagnostics;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Lifecycle;

/// <summary>Havuz olmadan, ham süreçle worker yaşam döngüsü.</summary>
[Trait("Category", "WorkerIntegration")]
public class WorkerLifecycleTests
{
    [Fact]
    public async Task ParentStdinClosed_WorkerExits()
    {
        var spec = WorkerTestEnvironment.ProductionFactory(WorkerTestEnvironment.Options()).Create();
        using var process = Process.Start(spec.StartInfo)!;
        try
        {
            var stdout = process.StandardOutput.BaseStream;
            var hello = await FrameCodec.ReadAsync(stdout, FrameLimits.MaxResponseFrameBytes).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(hello);
            Assert.Equal(FrameType.Hello, hello.Type);
            var header = WorkerWire.DecodeHeader(hello, WorkerJsonContext.Default.HelloHeader);
            Assert.Equal(ProtocolVersion.Current, header.Protocol);
            Assert.Equal(process.Id, header.Pid);

            // Ping çalışıyor → sonra Api'nin ölümünü taklit et: stdin kapanır.
            await WorkerWire.WriteRequestAsync(process.StandardInput.BaseStream, new RequestHeader { Id = "1", Op = WorkerOps.Ping }, []);
            var pong = await FrameCodec.ReadAsync(stdout, FrameLimits.MaxResponseFrameBytes).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(WorkerWire.DecodeHeader(pong!, WorkerJsonContext.Default.ResponseHeader).Ok);

            process.StandardInput.Close();

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            foreach (var dir in new[] { spec.WorkingDirectory, spec.TempDirectory })
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UnknownArgument_ExitsWithoutHello()
    {
        var spec = WorkerTestEnvironment.ProductionFactory(WorkerTestEnvironment.Options()).Create();
        spec.StartInfo.ArgumentList.Add("--no-such-flag");
        using var process = Process.Start(spec.StartInfo)!;
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(64, process.ExitCode);
            Assert.Null(await FrameCodec.ReadAsync(process.StandardOutput.BaseStream, FrameLimits.MaxResponseFrameBytes));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            foreach (var dir in new[] { spec.WorkingDirectory, spec.TempDirectory })
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }
}
