
using Amazon.S3;
using Amazon.S3.Model;
using Estuscia.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Estuscia.Infrastructure.Services;

public sealed class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3FileStorageService(
        IAmazonS3 s3,
        IConfiguration configuration)
    {
        _s3 = s3;

        _bucketName =
            configuration["AWS:S3:BucketName"]
            ?? throw new InvalidOperationException(
                "AWS S3 bucket name is not configured.");
    }

    public async Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        using var response = await _s3.GetObjectAsync(
            _bucketName,
            key,
            cancellationToken);

        var memoryStream = new MemoryStream();

        await response.ResponseStream.CopyToAsync(
            memoryStream,
            cancellationToken);

        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task DeleteAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await _s3.DeleteObjectAsync(
            _bucketName,
            key,
            cancellationToken);
    }

    public string GetPresignedUrl(
        string key,
        TimeSpan? expiresIn = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(
                expiresIn ?? TimeSpan.FromMinutes(15))
        };

        return _s3.GetPreSignedURL(request);
    }
}
