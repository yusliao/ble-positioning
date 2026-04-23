using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;

namespace BlePositioning.Tests;

/// <summary>测试宿主不写真实磁盘或写入临时目录；满足 <see cref="IFloorMapStorage"/> 契约。</summary>
internal sealed class TestFloorMapStorage : IFloorMapStorage
{
    public async Task<Result<string>> SaveAsync(
        Guid floorId,
        Stream content,
        string contentType,
        string? originalFileName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Fail("Unsupported image type.");
        }

        await content.CopyToAsync(Stream.Null, ct);
        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        return Result<string>.Ok($"/maps/{floorId:N}{ext}");
    }
}
