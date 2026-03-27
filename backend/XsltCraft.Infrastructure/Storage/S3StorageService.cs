using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Configuration;

namespace XsltCraft.Infrastructure.Storage;

/// <summary>
/// S3-uyumlu nesne deposu için IStorageService implementasyonu.
/// AWS S3, MinIO ve diğer S3-uyumlu depoları destekler.
///
/// Gerekli konfigürasyon:
///   Storage:S3BucketName  — hedef bucket adı
///   Storage:S3Region      — AWS region (ör. eu-central-1)
///
/// Opsiyonel konfigürasyon:
///   Storage:S3ServiceUrl  — MinIO / özel endpoint URL'i (S3-uyumlu servisler için)
///   Storage:S3AccessKey   — access key (IAM role kullanıyorsa gerekli değil)
///   Storage:S3SecretKey   — secret key (IAM role kullanıyorsa gerekli değil)
/// </summary>
public class S3StorageService : IStorageService, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;

    public S3StorageService(IConfiguration configuration)
    {
        _bucketName = configuration["Storage:S3BucketName"]
            ?? throw new InvalidOperationException("Storage:S3BucketName yapılandırılmamış.");

        var region = configuration["Storage:S3Region"]
            ?? throw new InvalidOperationException("Storage:S3Region yapılandırılmamış.");

        var accessKey  = configuration["Storage:S3AccessKey"];
        var secretKey  = configuration["Storage:S3SecretKey"];
        var serviceUrl = configuration["Storage:S3ServiceUrl"];

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        // MinIO veya özel S3-uyumlu endpoint kullanılıyorsa
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            s3Config.ServiceURL  = serviceUrl;
            s3Config.ForcePathStyle = true; // MinIO path-style erişim gerektirir
        }

        // Kimlik bilgisi sağlandıysa explicit credentials, aksi hâlde IAM rol / env zinciri
        _client = !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config)
            : new AmazonS3Client(s3Config);
    }

    /// <inheritdoc />
    public async Task<string> WriteAsync(Stream content, string relativePath, string contentType)
    {
        ValidatePath(relativePath);

        var request = new PutObjectRequest
        {
            BucketName  = _bucketName,
            Key         = relativePath,
            InputStream = content,
            ContentType = contentType
        };

        await _client.PutObjectAsync(request);
        return relativePath;
    }

    /// <inheritdoc />
    public async Task<Stream> ReadAsync(string relativePath)
    {
        ValidatePath(relativePath);

        try
        {
            var response = await _client.GetObjectAsync(_bucketName, relativePath);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Storage dosyası bulunamadı: {relativePath}");
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string relativePath)
    {
        ValidatePath(relativePath);

        await _client.DeleteObjectAsync(_bucketName, relativePath);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string relativePath)
    {
        ValidatePath(relativePath);

        try
        {
            await _client.GetObjectMetadataAsync(_bucketName, relativePath);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose() => _client.Dispose();

    // Path traversal koruması: S3 key'i ".." içermemeli
    private static void ValidatePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Storage path boş olamaz.", nameof(relativePath));

        if (relativePath.Contains(".."))
            throw new UnauthorizedAccessException("Geçersiz storage yolu.");
    }
}
