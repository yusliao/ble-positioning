using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Floors;
using BlePositioning.Application.Positioning;
using BlePositioning.Application.Geofence;
using BlePositioning.Infrastructure.Caching;
using BlePositioning.Infrastructure.Geofence;
using BlePositioning.Infrastructure.Options;
using BlePositioning.Infrastructure.Persistence;
using BlePositioning.Infrastructure.Persistence.BulkWriters;
using BlePositioning.Infrastructure.Persistence.Repositories;
using BlePositioning.Infrastructure.Devices;
using BlePositioning.Infrastructure.Positioning;
using BlePositioning.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BlePositioning.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiKeyOptions>().Bind(configuration.GetSection(ApiKeyOptions.SectionName));
        services.AddOptions<PositioningOptions>().Bind(configuration.GetSection(PositioningOptions.SectionName));
        services.AddOptions<RedisGeofenceStateOptions>().Bind(configuration.GetSection(RedisGeofenceStateOptions.SectionName));
        services.AddOptions<GeofenceWebhookOptions>().Bind(configuration.GetSection(GeofenceWebhookOptions.SectionName));
        services.AddOptions<DevicePresenceOptions>().Bind(configuration.GetSection(DevicePresenceOptions.SectionName));
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<IApiKeyHasher, ApiKeyHasher>();

        var pg = configuration.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
        services.AddDbContext<AppDbContext>(
            o => o.UseNpgsql(pg).UseSnakeCaseNamingConvention(),
            ServiceLifetime.Scoped,
            ServiceLifetime.Singleton);
        services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(pg).UseSnakeCaseNamingConvention());

        services.AddScoped<IFloorRepository, FloorRepository>();
        services.AddScoped<ITrackedDeviceRepository, TrackedDeviceRepository>();
        services.AddScoped<ITrajectoryQuery, NpgsqlTrajectoryQuery>();
        services.AddScoped<IFloorService, FloorService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IGeofenceEvaluationService, GeofenceEvaluationService>();
        services.AddScoped<IGeofenceEventQueryService, GeofenceEventQueryService>();
        services.AddScoped<IDevicePresenceEventWriter, DevicePresenceEventWriter>();
        services.AddScoped<IDevicePresenceEventQueryService, DevicePresenceEventQueryService>();

        services.AddSingleton<IBeaconLookup, BeaconLookup>();

        var redisCs = configuration.GetConnectionString("Redis")
                      ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
        services.AddSingleton<IKalmanStateStore>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<PositioningOptions>>().Value;
            if (opt.StoreKalmanStateInRedis)
            {
                return (IKalmanStateStore)new RedisKalmanStateStore(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    sp.GetRequiredService<ILogger<RedisKalmanStateStore>>());
            }

            return new InMemoryKalmanStateStore();
        });
        services.AddSingleton<IKalmanPositionFilter, KalmanPositionFilter>();
        services.AddSingleton<IPositionCache, RedisPositionCache>();
        services.AddSingleton<IGeofenceStateStore, RedisGeofenceStateStore>();
        services.AddSingleton<IGeofenceEventPublisher, NoOpGeofenceEventPublisher>();
        services.AddSingleton<IDevicePresenceLifecycleStore, RedisDevicePresenceLifecycleStore>();
        services.AddSingleton<IDevicePresenceCoordinator, DevicePresenceCoordinator>();
        services.AddSingleton<IDevicePresenceEventPublisher, NoOpDevicePresenceEventPublisher>();

        services.AddSingleton<RssiIngestChannel>();
        services.AddSingleton<IRssiReportQueue>(sp => sp.GetRequiredService<RssiIngestChannel>());

        services.AddSingleton<ITrajectoryBulkWriter, NpgsqlTrajectoryBulkWriter>();
        services.AddHostedService<PositioningPipelineService>();
        services.AddHostedService<DevicePresenceSweeperService>();

        return services;
    }
}
