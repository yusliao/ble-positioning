using BlePositioning.Application.Common.Models;

namespace BlePositioning.Application.Floors;

public interface IFloorMapStorage
{
    /// <summary>保存图片并返回可对浏览器访问的相对路径（以 / 开头，如 /maps/xxx.jpg）。</summary>
    Task<Result<string>> SaveAsync(
        Guid floorId,
        Stream content,
        string contentType,
        string? originalFileName,
        CancellationToken ct = default);
}
