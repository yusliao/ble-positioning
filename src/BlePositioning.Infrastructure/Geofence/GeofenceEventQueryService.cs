using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Geofence;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlePositioning.Infrastructure.Geofence;

public sealed class GeofenceEventQueryService(AppDbContext db) : IGeofenceEventQueryService
{
    public async Task<Result<IReadOnlyList<GeofenceEventDto>>> ListByDeviceAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken ct = default)
    {
        startTimeUtc = NormalizeUtc(startTimeUtc);
        endTimeUtc = NormalizeUtc(endTimeUtc);

        if (endTimeUtc < startTimeUtc)
            return Result<IReadOnlyList<GeofenceEventDto>>.Fail("endTime must be greater than or equal to startTime.");

        var deviceOk = await db.TrackedDevices.AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && !d.IsDeleted, ct);
        if (!deviceOk)
            return Result<IReadOnlyList<GeofenceEventDto>>.Fail("Device not found.");

        var rows = await db.GeofenceEvents.AsNoTracking()
            .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= startTimeUtc && e.OccurredAtUtc <= endTimeUtc)
            .OrderBy(e => e.OccurredAtUtc)
            .Take(10_000)
            .ToListAsync(ct);

        IReadOnlyList<GeofenceEventDto> list = rows
            .Select(e => new GeofenceEventDto(
                e.Id,
                e.DeviceId,
                e.FloorId,
                e.AlertRuleId,
                e.EventKind,
                e.X,
                e.Y,
                e.Accuracy,
                e.OccurredAtUtc))
            .ToList();

        return Result<IReadOnlyList<GeofenceEventDto>>.Ok(list);
    }

    private static DateTime NormalizeUtc(DateTime t) =>
        t.Kind switch
        {
            DateTimeKind.Utc => t,
            DateTimeKind.Local => t.ToUniversalTime(),
            _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
        };
}
