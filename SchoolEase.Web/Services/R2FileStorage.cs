using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Services;

/// <summary>
/// S3-compatible object storage. Points at Cloudflare R2 (10 GB free, no egress
/// fees) but works with any provider that speaks the S3 API, because the endpoint
/// and the credentials are read from configuration.
/// </summary>
public class R2FileStorage : IFileStorage
{
    private readonly StorageSettings _settings;
    private readonly ILogger<R2FileStorage> _logger;
    private readonly Lazy<IAmazonS3> _client;

    public R2FileStorage(IOptions<StorageSettings> settings, ILogger<R2FileStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        _client = new Lazy<IAmazonS3>(() =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = _settings.Endpoint,
                ForcePathStyle = true,
                // R2 ignores the region but the SDK requires one.
                AuthenticationRegion = "auto"
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey),
                config);
        }, isThreadSafe: true);
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<string> UploadAsync(
        string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";

        var key = $"{_settings.Prefix.Trim('/')}/las/{DateTime.UtcNow:yyyy}/{Guid.NewGuid():N}{extension}";

        var request = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true // R2 prefers unsigned (streaming) payloads
        };

        await _client.Value.PutObjectAsync(request, cancellationToken);
        _logger.LogInformation("Uploaded {Key} to bucket {Bucket}.", key, _settings.Bucket);
        return key;
    }

    public async Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _client.Value.GetObjectAsync(
            new GetObjectRequest { BucketName = _settings.Bucket, Key = key }, cancellationToken);

        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.Value.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = _settings.Bucket, Key = key }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            // A failed cleanup must never block deleting the record itself.
            _logger.LogWarning(ex, "Could not delete object {Key} from bucket {Bucket}.", key, _settings.Bucket);
        }
    }
}
