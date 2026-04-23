using BlePositioning.Application.Devices;
using BlePositioning.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BlePositioning.API.Positioning;

public sealed class SignalRDevicePresenceEventPublisher(IHubContext<PositioningHub> hubContext)
    : IDevicePresenceEventPublisher
{
    public Task PublishAsync(DevicePresenceEventNotification n, CancellationToken ct = default) =>
        hubContext.Clients.All.SendAsync(
            "DevicePresenceEvent",
            n.DeviceId,
            n.EventKind,
            n.OccurredAtUtc,
            cancellationToken: ct);
}
