namespace XsltCraft.Infrastructure.Storage;

public interface IStorageService
{
    Task<string> WriteAsync(Stream content, string relativePath, string contentType);
    Task<Stream> ReadAsync(string relativePath);
    Task DeleteAsync(string relativePath);
    Task<bool> ExistsAsync(string relativePath);
}
