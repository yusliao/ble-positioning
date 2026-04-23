namespace BlePositioning.Application.Devices;

/// <summary>Redis 中设备 «已判在线/已判离线» 边沿状态，键 <c>dpl:&#123;deviceId&#125;</c>，值 on|off。</summary>
public interface IDevicePresenceLifecycleStore
{
    Task<string?> GetAsync(Guid deviceId, CancellationToken ct = default);
    Task SetOnAsync(Guid deviceId, CancellationToken ct = default);
    Task SetOffAsync(Guid deviceId, CancellationToken ct = default);
}
