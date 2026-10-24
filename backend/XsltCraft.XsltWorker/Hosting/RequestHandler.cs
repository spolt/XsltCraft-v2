using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Op yönlendirici. W1: yalnız <c>ping</c>; <c>render</c>/<c>validate</c> motor bağlanana dek (W2)
/// <c>EngineUnavailable</c> döner. Test op'ları yalnız <see cref="TestOps"/> verildiyse (<c>--test-ops</c>) çalışır.
/// Hata mesajları genel tutulur; yığın izi/yol sızdırılmaz.
/// </summary>
internal sealed class RequestHandler
{
    private readonly TestOps? _testOps;

    public RequestHandler(TestOps? testOps)
    {
        _testOps = testOps;
    }

    public HandlerResult Handle(RequestHeader request, IReadOnlyList<byte[]> bodies)
    {
        try
        {
            return Dispatch(request, bodies);
        }
        catch (OutOfMemoryException)
        {
            // GC heap tavanı aşıldı: yanıt ver, havuzdan geri dönüşüm iste.
            var result = Failure(request.Id, WorkerFailureKinds.MemoryLimit, WorkerErrorCodes.OutOfMemory,
                "Worker bellek sınırına ulaştı.");
            return result with { Header = result.Header with { RecycleRequested = true } };
        }
        catch (Exception)
        {
            return Failure(request.Id, WorkerFailureKinds.EngineInternal, "worker.internal", "Worker iç hatası.");
        }
    }

    private HandlerResult Dispatch(RequestHeader request, IReadOnlyList<byte[]> bodies)
    {
        switch (request.Op)
        {
            case WorkerOps.Ping:
                return Ok(request.Id, HandlerResult.NoBodies);

            case WorkerOps.Render:
            case WorkerOps.Validate:
                return Failure(request.Id, WorkerFailureKinds.EngineUnavailable, WorkerErrorCodes.EngineNotLoaded,
                    $"'{request.Engine ?? "?"}' motoru bu worker sürümünde yüklü değil.");
        }

        if (_testOps is not null && _testOps.TryHandle(request, bodies, out var testResult))
            return testResult;

        return Failure(request.Id, WorkerFailureKinds.EngineInternal, WorkerErrorCodes.UnknownOp, "Bilinmeyen işlem.");
    }

    public static HandlerResult Ok(string id, IReadOnlyList<ReadOnlyMemory<byte>> bodies) =>
        new(Stamp(new ResponseHeader { Id = id, Ok = true }), bodies);

    public static HandlerResult Failure(string id, string kind, string errorCode, string message) =>
        new(Stamp(new ResponseHeader
        {
            Id = id,
            Ok = false,
            Failure = new ResponseFailure { Kind = kind, ErrorCode = errorCode, Message = message },
        }), HandlerResult.NoBodies);

    private static ResponseHeader Stamp(ResponseHeader header) => header with
    {
        WorkingSetBytes = Environment.WorkingSet,
        GcHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
    };
}
