using XsltCraft.Infrastructure.Storage;

namespace XsltCraft.XsltWorker.Tests.Support;

/// <summary>Her yolu sabit 3 baytla okuyan, hiçbir şey yazmayan test deposu.</summary>
internal sealed class FixedBytesStorage : IStorageService
{
    public Task<string> WriteAsync(Stream content, string relativePath, string contentType) => Task.FromResult(relativePath);
    public Task<Stream> ReadAsync(string relativePath) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    public Task DeleteAsync(string relativePath) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string relativePath) => Task.FromResult(true);
}
