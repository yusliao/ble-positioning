namespace BlePositioning.Application.Positioning;

public interface IPositionCache
{
    Task SetAsync(Guid deviceId, CachedPosition position, CancellationToken ct = default);
    Task<CachedPosition?> GetAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>与 «在线» 一致：<c>pos:&#123;deviceId&#125;</c> 在 Redis 中仍存在未过期时返回 true（见 <c>Positioning:PositionTtlSeconds</c>）。</summary>
    Task<bool> HasPositionKeyAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>批量探测 <c>pos:</c> 键存在性（设备列表 <see cref="Devices.DeviceSummaryDto.IsOnline"/>）。</summary>
    Task<IReadOnlyDictionary<Guid, bool>> GetPositionKeyExistsAsync(IReadOnlyList<Guid> deviceIds, CancellationToken ct = default);
}
