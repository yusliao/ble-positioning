using BlePositioning.Application.Common.Models;

namespace BlePositioning.Application.Floors;

public interface IFloorService
{
    Task<Result<FloorDto>> CreateAsync(CreateFloorRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FloorDto>> ListAsync(CancellationToken ct = default);
    Task<Result<FloorDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<FloorDto>> UpdateAsync(Guid id, UpdateFloorRequest request, CancellationToken ct = default);
    Task<Result<FloorDto>> UploadMapImageAsync(
        Guid id,
        Stream content,
        string contentType,
        string? originalFileName,
        CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>只读信标列表；楼层不存在时失败。</summary>
    Task<Result<IReadOnlyList<BeaconListItemDto>>> ListBeaconsByFloorAsync(Guid floorId, CancellationToken ct = default);

    Task<Result<BeaconListItemDto>> CreateBeaconAsync(Guid floorId, CreateBeaconRequest request, CancellationToken ct = default);
    Task<Result<BeaconListItemDto>> UpdateBeaconAsync(Guid floorId, Guid beaconId, UpdateBeaconRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteBeaconAsync(Guid floorId, Guid beaconId, CancellationToken ct = default);

    /// <summary>读楼层下的围栏规则。</summary>
    Task<Result<IReadOnlyList<AlertRuleListItemDto>>> ListAlertRulesByFloorAsync(
        Guid floorId,
        CancellationToken ct = default);

    Task<Result<AlertRuleListItemDto>> CreateAlertRuleAsync(
        Guid floorId,
        CreateAlertRuleRequest request,
        CancellationToken ct = default);

    Task<Result<AlertRuleListItemDto>> UpdateAlertRuleAsync(
        Guid floorId,
        Guid ruleId,
        UpdateAlertRuleRequest request,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteAlertRuleAsync(Guid floorId, Guid ruleId, CancellationToken ct = default);
}
