using BlePositioning.Application.Geofence;

namespace BlePositioning.Infrastructure.Geofence;

/// <summary>按顺序执行多个 <see cref="IGeofenceEventPublisher"/>（如 SignalR + Webhook）。</summary>
public sealed class CompositeGeofenceEventPublisher : IGeofenceEventPublisher
{
    private readonly IReadOnlyList<IGeofenceEventPublisher> _publishers;

    public CompositeGeofenceEventPublisher(params IGeofenceEventPublisher[] publishers)
    {
        ArgumentNullException.ThrowIfNull(publishers);
        _publishers = publishers;
    }

    public async Task PublishAsync(GeofenceEventNotification notification, CancellationToken ct = default)
    {
        foreach (var p in _publishers)
            await p.PublishAsync(notification, ct).ConfigureAwait(false);
    }
}
