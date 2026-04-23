using System.Collections.Concurrent;
using BlePositioning.Application.Positioning;

namespace BlePositioning.Infrastructure.Positioning;

/// <summary>单 API 进程内卡尔曼状态（ADR-008 单实例路径）。</summary>
public sealed class InMemoryKalmanStateStore : IKalmanStateStore
{
    private readonly ConcurrentDictionary<Guid, KalmanFilterState> _mem = new();

    public Task<KalmanFilterState?> GetAsync(Guid deviceId, CancellationToken ct = default) =>
        Task.FromResult(_mem.TryGetValue(deviceId, out var s) ? s : null);

    public Task SetAsync(Guid deviceId, KalmanFilterState state, TimeSpan ttl, CancellationToken ct = default)
    {
        _mem[deviceId] = state;
        return Task.CompletedTask;
    }
}
