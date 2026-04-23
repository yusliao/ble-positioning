using BlePositioning.Application.Devices;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BlePositioning.Infrastructure.Devices;

public sealed class RedisDevicePresenceLifecycleStore(
    IConnectionMultiplexer redis,
    IOptions<DevicePresenceOptions> options) : IDevicePresenceLifecycleStore
{
    private static string Key(Guid deviceId) => $"dpl:{deviceId}";

    public async Task<string?> GetAsync(Guid deviceId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var v = await db.StringGetAsync(Key(deviceId)).WaitAsync(ct);
        return v.IsNullOrEmpty ? null : v.ToString();
    }

    public Task SetOnAsync(Guid deviceId, CancellationToken ct = default) =>
        SetAsync(deviceId, "on", ct);

    public Task SetOffAsync(Guid deviceId, CancellationToken ct = default) =>
        SetAsync(deviceId, "off", ct);

    private async Task SetAsync(Guid deviceId, string value, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var ttl = options.Value.StateKeyTtl;
        await db.StringSetAsync(Key(deviceId), value, ttl).WaitAsync(ct);
    }
}
