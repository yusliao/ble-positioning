using System.Text.Json;
using BlePositioning.Application.Positioning;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BlePositioning.Infrastructure.Caching;

public sealed class RedisPositionCache(
    IConnectionMultiplexer redis,
    IOptions<PositioningOptions> options) : IPositionCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task SetAsync(Guid deviceId, CachedPosition position, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = $"pos:{deviceId}";
        var json = JsonSerializer.Serialize(position, JsonOptions);
        var ttl = TimeSpan.FromSeconds(options.Value.PositionTtlSeconds);
        await db.StringSetAsync(key, json, ttl).WaitAsync(ct);
    }

    public async Task<CachedPosition?> GetAsync(Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = await db.StringGetAsync($"pos:{deviceId}").WaitAsync(ct);
            return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<CachedPosition>(json!, JsonOptions);
        }
        catch (RedisConnectionException)
        {
            return null;
        }
    }

    public async Task<bool> HasPositionKeyAsync(Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            return await db.KeyExistsAsync($"pos:{deviceId}").WaitAsync(ct);
        }
        catch (RedisConnectionException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetPositionKeyExistsAsync(
        IReadOnlyList<Guid> deviceIds,
        CancellationToken ct = default)
    {
        if (deviceIds.Count == 0)
            return new Dictionary<Guid, bool>();

        var result = new Dictionary<Guid, bool>(deviceIds.Count);
        try
        {
            var db = redis.GetDatabase();
            foreach (var id in deviceIds)
            {
                var ok = await db.KeyExistsAsync($"pos:{id}").WaitAsync(ct);
                result[id] = ok;
            }
        }
        catch (RedisConnectionException)
        {
            foreach (var id in deviceIds)
                result[id] = false;
        }

        return result;
    }
}
