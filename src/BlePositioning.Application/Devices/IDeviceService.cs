using BlePositioning.Application.Common.Models;

namespace BlePositioning.Application.Devices;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(CancellationToken ct = default);

    Task<Result<CreateTrackedDeviceResult>> CreateWithApiKeyAsync(CreateTrackedDeviceRequest request, CancellationToken ct = default);
    Task<Result<DevicePositionDto>> GetLatestPositionAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>历史轨迹；参数校验失败或桶数超限返回 Fail（由 API 映射为 400）。</summary>
    Task<Result<DeviceTrajectoryDto>> GetTrajectoryAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Guid? floorId,
        int intervalSeconds,
        CancellationToken ct = default);
}
