using System.Text.Json;
using BlePositioning.Application.Positioning;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BlePositioning.Infrastructure.Positioning;

/// <summary>多实例部署时卡尔曼状态外置 Redis，键 <c>device:&#123;id&#125;:kalman</c>（ADR-008）。</summary>
public sealed class RedisKalmanStateStore(
    IConnectionMultiplexer redis,
    ILogger<RedisKalmanStateStore> logger) : IKalmanStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string Key(Guid deviceId) => $"device:{deviceId}:kalman";

    public async Task<KalmanFilterState?> GetAsync(Guid deviceId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = await db.StringGetAsync(Key(deviceId)).WaitAsync(ct);
            if (json.IsNullOrEmpty)
                return null;
            return JsonSerializer.Deserialize<KalmanFilterState>(json!, JsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis kalman get failed for {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task SetAsync(Guid deviceId, KalmanFilterState state, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await db.StringSetAsync(Key(deviceId), json, ttl).WaitAsync(ct);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis kalman set failed for {DeviceId}", deviceId);
        }
    }
}
