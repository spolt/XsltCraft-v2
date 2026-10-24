using System.Reflection;
using XsltCraft.XsltWorker.Contracts;
using XsltCraft.XsltWorker.Contracts.Framing;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Host döngüsü: Hello gönder → stdin'den Request oku → <see cref="EngineThread"/>'e ver, bekle → Response yaz.
/// Sıralı: worker başına aynı anda tek istek. stdin EOF (Api kapandı/öldü) veya Shutdown → temiz çıkış;
/// bellek tükenmesi yanıtından sonra <see cref="WorkerExitCodes.MemoryExhausted"/>.
/// </summary>
internal sealed class WorkerHost
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly RequestHandler _handler;
    private readonly EngineThread _engineThread;
    private readonly WorkerCommandLine _commandLine;
    private readonly string? _saxonVersion;

    public WorkerHost(
        Stream input, Stream output, RequestHandler handler, EngineThread engineThread, WorkerCommandLine commandLine, string? saxonVersion)
    {
        _input = input;
        _output = output;
        _handler = handler;
        _engineThread = engineThread;
        _commandLine = commandLine;
        _saxonVersion = saxonVersion;
    }

    public async Task<int> RunAsync()
    {
        try
        {
            await WorkerWire.WriteHelloAsync(_output, BuildHello());
        }
        catch (IOException)
        {
            return WorkerExitCodes.OutputClosed;
        }

        while (true)
        {
            Frame? frame;
            RequestHeader request;
            try
            {
                frame = await FrameCodec.ReadAsync(_input, FrameLimits.MaxRequestFrameBytes);
                if (frame is null || frame.Type == FrameType.Shutdown)
                    return WorkerExitCodes.Normal;
                if (frame.Type != FrameType.Request)
                    return WorkerExitCodes.ProtocolError;

                request = WorkerWire.DecodeHeader(frame, WorkerJsonContext.Default.RequestHeader);
            }
            catch (FrameProtocolException)
            {
                return WorkerExitCodes.ProtocolError;
            }
            catch (IOException)
            {
                return WorkerExitCodes.Normal;
            }

            var bodies = frame.Bodies;
            var result = await _engineThread.RunAsync(() => _handler.Handle(request, bodies));

            try
            {
                await WriteResponseAsync(request, result);
            }
            catch (IOException)
            {
                return WorkerExitCodes.OutputClosed;
            }

            if (result.ExitAfterResponse)
                return WorkerExitCodes.MemoryExhausted;
        }
    }

    private async Task WriteResponseAsync(RequestHeader request, HandlerResult result)
    {
        try
        {
            await WorkerWire.WriteResponseAsync(_output, result.Header, result.Bodies);
        }
        catch (FrameProtocolException ex) when (ex.Error is FrameError.FrameTooLarge or FrameError.HeaderTooLarge)
        {
            // Codec sınır ihlalinde hiçbir bayt yazmaz → akış sağlam; çıktı yerine hata dönülür.
            var failure = RequestHandler.Failure(request.Id, WorkerFailureKinds.OutputLimit, "output_frame_too_large",
                "Çıktı, yanıt çerçevesi sınırını aşıyor.");
            await WorkerWire.WriteResponseAsync(_output, failure.Header, failure.Bodies);
        }
    }

    private HelloHeader BuildHello() => new()
    {
        Protocol = _commandLine.HelloProtocolOverride ?? ProtocolVersion.Current,
        WorkerVersion = typeof(WorkerHost).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
        SaxonVersion = _saxonVersion,
        Pid = Environment.ProcessId,
    };
}
