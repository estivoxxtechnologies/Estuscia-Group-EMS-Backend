
using Microsoft.AspNetCore.Http;
namespace Estuscia.WebApi.Security;

public static class UploadedFileSignatureValidator
{
    public static async Task<bool> IsValidAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length < 4)
            return false;

        var header = new byte[12];

        await using var stream = file.OpenReadStream();

        var bytesRead = await stream.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" =>
                bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,

            ".png" =>
                bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A,

            ".webp" =>
                bytesRead >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P',

            ".pdf" =>
                bytesRead >= 5 &&
                header[0] == '%' &&
                header[1] == 'P' &&
                header[2] == 'D' &&
                header[3] == 'F' &&
                header[4] == '-',

            _ => false
        };
    }
}