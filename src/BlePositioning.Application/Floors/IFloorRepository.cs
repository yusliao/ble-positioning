using BlePositioning.Domain;

namespace BlePositioning.Application.Floors;

public interface IFloorRepository
{
    /// <summary>快速存在性检查（不加载导航属性）。</summary>
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>按楼层列未软删信标，按 UUID/Major/Minor 排序。</summary>
    Task<IReadOnlyList<Beacon>> ListActiveBeaconsByFloorAsync(Guid floorId, CancellationToken ct = default);

    Task<Floor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Floor>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Floor floor, CancellationToken ct = default);
    Task AddBeaconAsync(Beacon beacon, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>未软删信标在全局 (uuid, major, minor) 上是否已存在。</summary>
    Task<bool> BeaconIdentityInUseAsync(string uuid, int major, int minor, CancellationToken ct = default);

    Task<Beacon?> GetTrackedBeaconAsync(Guid floorId, Guid beaconId, CancellationToken ct = default);

    /// <summary>按楼层读围栏规则（可空表）。</summary>
    Task<IReadOnlyList<AlertRule>> ListAlertRulesByFloorIdAsync(Guid floorId, CancellationToken ct = default);

    Task<AlertRule?> GetAlertRuleAsync(Guid floorId, Guid ruleId, CancellationToken ct = default);

    Task AddAlertRuleAsync(AlertRule rule, CancellationToken ct = default);

    void RemoveAlertRule(AlertRule rule);
}
