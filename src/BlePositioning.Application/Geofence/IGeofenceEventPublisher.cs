namespace BlePositioning.Application.Geofence;

public sealed record GeofenceEventNotification(
    Guid DeviceId,
    Guid FloorId,
    Guid AlertRuleId,
    short EventKind,
    double X,
    double Y,
    DateTime OccurredAtUtc);

/// <summary>阶段 C+：进/出区事件落库后经 Hub 等推送；阶段 D 可经 HTTP Webhook 再投递；测试可用空实现。</summary>
public interface IGeofenceEventPublisher
{
    Task PublishAsync(GeofenceEventNotification notification, CancellationToken ct = default);
}
