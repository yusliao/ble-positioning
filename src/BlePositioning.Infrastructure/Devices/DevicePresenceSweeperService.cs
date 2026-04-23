using BlePositioning.Application.Devices;
using BlePositioning.Application.Positioning;
using BlePositioning.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BlePositioning.Infrastructure.Options;
using IHostEnv = Microsoft.Extensions.Hosting.IHostEnvironment;

namespace BlePositioning.Infrastructure.Devices;

public sealed class DevicePresenceSweeperService(
    IServiceScopeFactory scopeFactory,
    IOptions<DevicePresenceOptions> options,
    IHostEnv environment,
    ILogger<DevicePresenceSweeperService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            return;

        var interval = options.Value.SweepInterval;
        if (interval < TimeSpan.FromSeconds(1))
            interval = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                var repository = sp.GetRequiredService<ITrackedDeviceRepository>();
                var positionCache = sp.GetRequiredService<IPositionCache>();
                var lifecycle = sp.GetRequiredService<IDevicePresenceLifecycleStore>();
                var writer = sp.GetRequiredService<IDevicePresenceEventWriter>();

                var devices = await repository.ListAsync(stoppingToken);
                var now = DateTime.UtcNow;
                foreach (var d in devices)
                {
                    if (d.IsDeleted)
                        continue;
                    var hasPos = await positionCache.HasPositionKeyAsync(d.Id, stoppingToken);
                    var s = await lifecycle.GetAsync(d.Id, stoppingToken);
                    if (hasPos)
                    {
                        if (string.Equals(s, "off", StringComparison.Ordinal))
                        {
                            await writer.WriteIfNeededAsync(
                                DevicePresenceEvent.KindOnline,
                                d.Id,
                                now,
                                stoppingToken);
                        }

                        if (s is null || string.Equals(s, "off", StringComparison.Ordinal))
                            await lifecycle.SetOnAsync(d.Id, stoppingToken);
                    }
                    else
                    {
                        if (string.Equals(s, "on", StringComparison.Ordinal))
                        {
                            await writer.WriteIfNeededAsync(
                                DevicePresenceEvent.KindOffline,
                                d.Id,
                                now,
                                stoppingToken);
                            await lifecycle.SetOffAsync(d.Id, stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Device presence sweep failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
