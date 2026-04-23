namespace BlePositioning.Application.Geofence;

/// <summary>在定位点更新后，对本楼层已启用围栏规则做进/出区判定与事件落库。</summary>
public interface IGeofenceEvaluationService
{
    Task EvaluateAsync(
        Guid deviceId,
        Guid floorId,
        double x,
        double y,
        double accuracy,
        DateTime occurredAtUtc,
        CancellationToken ct = default);
}
