using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Threadia.Modules.Attachments.Application;

namespace Threadia.Modules.Attachments.Infrastructure;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"S3" または "InMemory"(テスト用)。</summary>
    public string Provider { get; set; } = "S3";

    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "threadia-attachments";
}

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly StorageOptions _options;
    private readonly Protocol _presignProtocol;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketEnsured;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        // 署名付き URL のスキームは ServiceUrl に合わせる(SDK の既定は https 固定のため)。
        _presignProtocol = _options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;
        _client = new AmazonS3Client(
            _options.AccessKey,
            _options.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                // MinIO はパススタイルのみ対応。
                ForcePathStyle = true,
            });
    }

    public async Task<string> GetUploadUrlAsync(string key, string contentType, TimeSpan expiry, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType,
            Protocol = _presignProtocol,
        };
        return await _client.GetPreSignedURLAsync(request);
    }

    public async Task<string> GetDownloadUrlAsync(string key, string fileName, TimeSpan expiry, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = _presignProtocol,
        };
        request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
        return await _client.GetPreSignedURLAsync(request);
    }

    public async Task<long?> GetObjectSizeAsync(string key, CancellationToken ct)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_options.Bucket, key, ct);
            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await _client.DeleteObjectAsync(_options.Bucket, key, ct);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _bucketLock.WaitAsync(ct);
        try
        {
            if (!_bucketEnsured)
            {
                try
                {
                    await _client.PutBucketAsync(_options.Bucket, ct);
                }
                catch (AmazonS3Exception ex) when (
                    ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
                {
                    // 既に存在すれば何もしない。
                }

                _bucketEnsured = true;
            }
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _bucketLock.Dispose();
    }
}
