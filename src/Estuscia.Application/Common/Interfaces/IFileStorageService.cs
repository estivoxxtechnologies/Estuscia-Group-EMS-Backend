
namespace Estuscia.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string key,
        CancellationToken cancellationToken = default);

    string GetPresignedUrl(
        string key,
        TimeSpan? expiresIn = null);
}
