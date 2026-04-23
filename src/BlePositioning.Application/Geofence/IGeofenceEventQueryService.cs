using BlePositioning.Application.Common.Models;

namespace BlePositioning.Application.Geofence;

public interface IGeofenceEventQueryService
{
    Task<Result<IReadOnlyList<GeofenceEventDto>>> ListByDeviceAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken ct = default);
}
