using BlePositioning.Application.Devices;
using BlePositioning.Application.Positioning;
using BlePositioning.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlePositioning.Infrastructure.Devices;

public sealed class DevicePresenceCoordinator(
    IServiceScopeFactory scopeFactory,
    IDevicePresenceLifecycleStore lifecycle,
    ILogger<DevicePresenceCoordinator> logger) : IDevicePresenceCoordinator
{
    public async Task OnPositionCacheUpdatedAsync(
        Guid deviceId,
        DateTime reportedAtUtc,
        CancellationToken ct = default)
    {
        if (reportedAtUtc.Kind != DateTimeKind.Utc)
            reportedAtUtc = reportedAtUtc.ToUniversalTime();

        var state = await lifecycle.GetAsync(deviceId, ct);
        if (string.Equals(state, "off", StringComparison.Ordinal))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var writer = scope.ServiceProvider.GetRequiredService<IDevicePresenceEventWriter>();
                await writer.WriteIfNeededAsync(
                    DevicePresenceEvent.KindOnline,
                    deviceId,
                    reportedAtUtc,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Device presence online event write failed for {DeviceId}",
                    deviceId);
            }
        }

        try
        {
            await lifecycle.SetOnAsync(deviceId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Device presence SetOn failed for {DeviceId}", deviceId);
        }
    }
}
