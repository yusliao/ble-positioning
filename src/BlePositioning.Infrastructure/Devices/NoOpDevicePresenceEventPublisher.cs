using BlePositioning.Application.Devices;

namespace BlePositioning.Infrastructure.Devices;

public sealed class NoOpDevicePresenceEventPublisher : IDevicePresenceEventPublisher
{
    public Task PublishAsync(DevicePresenceEventNotification notification, CancellationToken ct = default) =>
        Task.CompletedTask;
}
