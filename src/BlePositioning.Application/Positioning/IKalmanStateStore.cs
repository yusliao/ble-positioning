namespace BlePositioning.Application.Positioning;

/// <summary>卡尔曼滤波器状态存取（单实例用内存、多实例用 Redis，见 ADR-008）。</summary>
public interface IKalmanStateStore
{
    Task<KalmanFilterState?> GetAsync(Guid deviceId, CancellationToken ct = default);
    Task SetAsync(Guid deviceId, KalmanFilterState state, TimeSpan ttl, CancellationToken ct = default);
}
