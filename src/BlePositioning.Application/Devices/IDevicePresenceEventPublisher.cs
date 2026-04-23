namespace BlePositioning.Application.Devices;

public sealed record DevicePresenceEventNotification(Guid DeviceId, short EventKind, DateTime OccurredAtUtc);

/// <summary>在线/离线事件推送到 Hub 等（阶段 E）。</summary>
public interface IDevicePresenceEventPublisher
{
    Task PublishAsync(DevicePresenceEventNotification notification, CancellationToken ct = default);
}
