using BlePositioning.Application.Geofence;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BlePositioning.Infrastructure.Geofence;

public sealed class RedisGeofenceStateStore(
    IConnectionMultiplexer redis,
    IOptions<RedisGeofenceStateOptions> options) : IGeofenceStateStore
{
    private static string Key(Guid deviceId, Guid ruleId) => $"gfstate:{deviceId:N}:{ruleId:N}";

    public async Task<bool> GetWasInsideAsync(Guid deviceId, Guid ruleId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var v = await db.StringGetAsync(Key(deviceId, ruleId)).WaitAsync(ct);
        if (v.IsNullOrEmpty)
            return false;
        return v == "1";
    }

    public async Task SetWasInsideAsync(Guid deviceId, Guid ruleId, bool wasInside, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(Key(deviceId, ruleId), wasInside ? "1" : "0", options.Value.Ttl).WaitAsync(ct);
    }
}
