using BlePositioning.Application.Geofence;

namespace BlePositioning.Infrastructure.Geofence;

public sealed class NoOpGeofenceEventPublisher : IGeofenceEventPublisher
{
    public Task PublishAsync(GeofenceEventNotification notification, CancellationToken ct = default) =>
        Task.CompletedTask;
}
