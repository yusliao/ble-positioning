using BlePositioning.API.Options;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;
using Microsoft.Extensions.Options;

namespace BlePositioning.API.Storage;

public sealed class WebHostFloorMapStorage(
    IWebHostEnvironment env,
    IOptions<FloorMapStorageOptions> options) : IFloorMapStorage
{
    public async Task<Result<string>> SaveAsync(
        Guid floorId,
        Stream content,
        string contentType,
        string? originalFileName,
        CancellationToken ct = default)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(contentType)
            || !opt.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<string>.Fail("Unsupported image type. Use JPEG, PNG, WebP, or GIF.");
        }

        var ext = ExtensionForContentType(contentType);
        if (ext is null)
            return Result<string>.Fail("Unsupported image type.");

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, opt.WebRelativeFolder.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folder);

        var fileName = $"{floorId:N}{ext}";
        var physicalPath = Path.Combine(folder, fileName);

        await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[8192];
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                total += read;
                if (total > opt.MaxBytes)
                    return Result<string>.Fail($"File exceeds maximum size of {opt.MaxBytes} bytes.");

                await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        var urlPath = "/" + string.Join('/', opt.WebRelativeFolder.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Concat(new[] { fileName }));
        return Result<string>.Ok(urlPath);
    }

    private static string? ExtensionForContentType(string contentType)
    {
        var ct = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return ct switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => null,
        };
    }
}
