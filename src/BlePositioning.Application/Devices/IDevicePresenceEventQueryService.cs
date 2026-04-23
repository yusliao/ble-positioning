using BlePositioning.Application.Common.Models;

namespace BlePositioning.Application.Devices;

public interface IDevicePresenceEventQueryService
{
    Task<Result<IReadOnlyList<DevicePresenceEventDto>>> ListByDeviceAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken ct = default);
}
