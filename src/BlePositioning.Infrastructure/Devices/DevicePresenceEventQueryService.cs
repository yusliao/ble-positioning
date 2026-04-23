using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Devices;
using BlePositioning.Infrastructure.Persistence;
using BlePositioning.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlePositioning.Infrastructure.Devices;

public sealed class DevicePresenceEventQueryService(
    AppDbContext db,
    IOptions<DevicePresenceOptions> options) : IDevicePresenceEventQueryService
{
    public async Task<Result<IReadOnlyList<DevicePresenceEventDto>>> ListByDeviceAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken ct = default)
    {
        var start = Normalize(startTimeUtc);
        var end = Normalize(endTimeUtc);
        if (end < start)
            return Result<IReadOnlyList<DevicePresenceEventDto>>.Fail("endTime must be greater than or equal to startTime.");

        var device = await db.TrackedDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null || device.IsDeleted)
            return Result<IReadOnlyList<DevicePresenceEventDto>>.Fail("Device not found.");

        var max = options.Value.QueryMaxEvents;
        var rows = await db.DevicePresenceEvents.AsNoTracking()
            .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= start && e.OccurredAtUtc <= end)
            .OrderBy(e => e.OccurredAtUtc)
            .Take(max + 1)
            .ToListAsync(ct);

        if (rows.Count > max)
            return Result<IReadOnlyList<DevicePresenceEventDto>>.Fail(
                $"At most {max} events can be returned. Narrow the time range.");

        IReadOnlyList<DevicePresenceEventDto> list = rows
            .Select(e => new DevicePresenceEventDto(e.Id, e.DeviceId, e.EventKind, e.OccurredAtUtc))
            .ToList();

        return Result<IReadOnlyList<DevicePresenceEventDto>>.Ok(list);
    }

    private static DateTime Normalize(DateTime t) =>
        t.Kind switch
        {
            DateTimeKind.Utc => t,
            DateTimeKind.Local => t.ToUniversalTime(),
            _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
        };
}
