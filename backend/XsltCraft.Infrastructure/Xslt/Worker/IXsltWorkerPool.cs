namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>
/// <see cref="XsltWorkerPool.ExecuteAsync"/>'in test edilebilir yüzü (<see cref="WorkerXsltHost"/> bunu kullanır).
/// Havuz sınıfının kendisi değiştirilmez; <see cref="XsltWorkerPoolAdapter"/> yönlendirir.
/// </summary>
public interface IXsltWorkerPool
{
    Task<XsltWorkerPoolResult> ExecuteAsync(XsltWorkerRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Singleton <see cref="XsltWorkerPool"/>'u <see cref="IXsltWorkerPool"/> olarak sunar.</summary>
internal sealed class XsltWorkerPoolAdapter(XsltWorkerPool pool) : IXsltWorkerPool
{
    public Task<XsltWorkerPoolResult> ExecuteAsync(XsltWorkerRequest request, CancellationToken cancellationToken = default) =>
        pool.ExecuteAsync(request, cancellationToken);
}
