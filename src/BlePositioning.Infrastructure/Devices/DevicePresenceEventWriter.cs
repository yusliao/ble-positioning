using BlePositioning.Application.Devices;
using BlePositioning.Domain;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace BlePositioning.Infrastructure.Devices;

public sealed class DevicePresenceEventWriter(
    AppDbContext db,
    IDevicePresenceEventPublisher publisher,
    ILogger<DevicePresenceEventWriter> logger) : IDevicePresenceEventWriter
{
    public async Task WriteIfNeededAsync(
        short eventKind,
        Guid deviceId,
        DateTime occurredAtUtc,
        CancellationToken ct = default)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            occurredAtUtc = occurredAtUtc.ToUniversalTime();

        var row = DevicePresenceEvent.Create(deviceId, eventKind, occurredAtUtc);
        db.DevicePresenceEvents.Add(row);
        await db.SaveChangesAsync(ct);
        try
        {
            await publisher.PublishAsync(
                new DevicePresenceEventNotification(deviceId, eventKind, occurredAtUtc),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Device presence event publish failed for {DeviceId}", deviceId);
        }
    }
}
