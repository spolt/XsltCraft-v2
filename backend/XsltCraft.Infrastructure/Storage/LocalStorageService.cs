using Microsoft.Extensions.Configuration;

namespace XsltCraft.Infrastructure.Storage;

public class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(IConfiguration configuration)
    {
        var rawPath = configuration["Storage:LocalBasePath"]
            ?? throw new InvalidOperationException("Storage:LocalBasePath is not configured.");

        _basePath = Path.GetFullPath(rawPath);
    }

    public async Task<string> WriteAsync(Stream content, string relativePath, string contentType)
    {
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file);

        return relativePath;
    }

    public Task<Stream> ReadAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Storage dosyası bulunamadı: {relativePath}", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    private string GetFullPath(string relativePath)
    {
        // Path traversal koruması: normalleştirilmiş path _basePath içinde kalmalı
        var full = Path.GetFullPath(Path.Combine(_basePath, relativePath));

        if (!full.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Geçersiz storage yolu.");

        return full;
    }
}
