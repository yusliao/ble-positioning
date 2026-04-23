using BlePositioning.API.Hubs;
using BlePositioning.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BlePositioning.API.Positioning;

public sealed class SignalRPositioningNotificationService(IHubContext<PositioningHub> hubContext) : IPositioningNotificationService
{
    public Task NotifyPositionUpdatedAsync(
        Guid floorId,
        Guid deviceId,
        double x,
        double y,
        double accuracy,
        CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(PositioningHub.FloorGroupName(floorId))
            .SendAsync("PositionUpdated", deviceId, x, y, accuracy, floorId, cancellationToken);
}
