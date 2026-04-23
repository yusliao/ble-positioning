using BlePositioning.Application.Geofence;
using BlePositioning.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BlePositioning.API.Positioning;

public sealed class SignalRGeofenceEventPublisher(IHubContext<PositioningHub> hubContext) : IGeofenceEventPublisher
{
    public Task PublishAsync(GeofenceEventNotification n, CancellationToken ct = default) =>
        hubContext.Clients
            .Group(PositioningHub.FloorGroupName(n.FloorId))
            .SendAsync(
                "GeofenceEvent",
                n.DeviceId,
                n.FloorId,
                n.AlertRuleId,
                n.EventKind,
                n.X,
                n.Y,
                n.OccurredAtUtc,
                cancellationToken: ct);
}
