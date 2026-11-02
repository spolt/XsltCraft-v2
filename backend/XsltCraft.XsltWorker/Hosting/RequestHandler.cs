using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Op yönlendirici (motor thread'inde çağrılır). <c>ping</c>; <c>render</c>/<c>validate</c> →
/// <see cref="XsltRequestRunner"/>. Test op'ları (ve taramasız <c>render-unscreened</c>) yalnız
/// <see cref="TestOps"/> verildiyse (<c>--test-ops</c>) çalışır.
/// <list type="bullet">
/// <item>Bellek tükenmesi (.NET <see cref="OutOfMemoryException"/> ya da istisna/cause zincirinde
///   <c>java.lang.OutOfMemoryError</c>) → <c>MemoryLimit</c> + <c>recycleRequested</c>; yanıttan sonra süreç çıkar.</item>
/// <item>Beklenmeyen .NET istisnası / <c>java.lang.Throwable</c> → <c>EngineInternal</c>, jenerik mesaj
///   (yığın izi, yol, içerik sızdırılmaz).</item>
/// </list>
/// </summary>
internal sealed class RequestHandler
{
    private const int MaxChainDepth = 32;

    private readonly XsltRequestRunner _runner;
    private readonly CompileCache _cache;
    private readonly TestOps? _testOps;

    public RequestHandler(WorkerEngineSet engines, CompileCache cache, TestOps? testOps)
    {
        ArgumentNullException.ThrowIfNull(engines);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _runner = new XsltRequestRunner(engines, cache);
        _testOps = testOps;
    }

    public HandlerResult Handle(RequestHeader request, IReadOnlyList<byte[]> bodies)
    {
        try
        {
            return Dispatch(request, bodies);
        }
        catch (Exception ex) when (IsOutOfMemory(ex))
        {
            return OutOfMemory(request.Id);
        }
        catch (InsufficientExecutionStackException)
        {
            return Failure(request.Id, WorkerFailureKinds.LimitExceeded, WorkerHostErrorCodes.StackLimit,
                "XSLT işlemi yığın derinliği sınırını aştı.");
        }
        catch (Exception)
        {
            return Failure(request.Id, WorkerFailureKinds.EngineInternal, WorkerHostErrorCodes.Internal, "Worker iç hatası.");
        }
    }

    private HandlerResult Dispatch(RequestHeader request, IReadOnlyList<byte[]> bodies)
    {
        switch (request.Op)
        {
            case WorkerOps.Ping:
                return Ok(request.Id, HandlerResult.NoBodies);

            case WorkerOps.Render:
                return _runner.Run(request, bodies, render: true, screen: true);

            case WorkerOps.Validate:
                return _runner.Run(request, bodies, render: false, screen: true);

            case TestOps.RenderUnscreened when _testOps is not null:
                // YALNIZ --test-ops: XsltSafety atlanır (T2.3 izolasyon testleri sandbox/listener/kill'i ölçer).
                return _runner.Run(request, bodies, render: true, screen: false);
        }

        if (_testOps is not null && _testOps.TryHandle(request, bodies, out var testResult))
            return testResult;

        return Failure(request.Id, WorkerFailureKinds.EngineInternal, WorkerErrorCodes.UnknownOp, "Bilinmeyen işlem.");
    }

    private HandlerResult OutOfMemory(string id)
    {
        // Önbellekteki executable'ları bırak; yanıtı kurabilecek kadar bellek kalsın.
        _cache.Clear();
        GC.Collect();

        var result = Failure(id, WorkerFailureKinds.MemoryLimit, WorkerErrorCodes.OutOfMemory, "Worker bellek sınırına ulaştı.");
        return result with { Header = result.Header with { RecycleRequested = true }, ExitAfterResponse = true };
    }

    /// <summary>
    /// <paramref name="ex"/> ya da iç istisna / Java cause zincirinde bellek tükenmesi var mı. Saxon (IKVM)
    /// .NET OOM'u <c>java.lang.OutOfMemoryError</c>'a çevirip sarabilir; motor bunu <c>EngineInternal</c>
    /// teşhisine dönüştürse bile zincirde kalır.
    /// </summary>
    public static bool IsOutOfMemory(Exception ex)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        for (Exception? cur = ex; cur is not null && seen.Count < MaxChainDepth && seen.Add(cur); cur = Next(cur))
        {
            if (cur is OutOfMemoryException or java.lang.OutOfMemoryError)
                return true;
        }

        return false;
    }

    private static Exception? Next(Exception ex)
    {
        if (ex is java.lang.Throwable t && t.getCause() is Exception cause && !ReferenceEquals(cause, ex))
            return cause;
        return ex.InnerException;
    }

    public static HandlerResult Ok(string id, IReadOnlyList<ReadOnlyMemory<byte>> bodies) =>
        new(Stamp(new ResponseHeader { Id = id, Ok = true }), bodies);

    public static HandlerResult Failure(string id, string kind, string errorCode, string message) =>
        Failure(id, new ResponseFailure { Kind = kind, ErrorCode = errorCode, Message = message });

    public static HandlerResult Failure(string id, ResponseFailure failure) =>
        new(Stamp(new ResponseHeader { Id = id, Ok = false, Failure = failure }), HandlerResult.NoBodies);

    private static ResponseHeader Stamp(ResponseHeader header) => header with
    {
        WorkingSetBytes = Environment.WorkingSet,
        GcHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
    };
}
