namespace BlePositioning.Application.Devices;

/// <summary>定位管道在写入 Redis 当前位姿后调用；产生「恢复在线」边沿（曾判离线后再次上报）。</summary>
public interface IDevicePresenceCoordinator
{
    Task OnPositionCacheUpdatedAsync(Guid deviceId, DateTime reportedAtUtc, CancellationToken ct = default);
}
