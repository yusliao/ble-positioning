namespace BlePositioning.Application.Geofence;

public sealed record GeofenceEventDto(
    Guid Id,
    Guid DeviceId,
    Guid FloorId,
    Guid AlertRuleId,
    short EventKind,
    double X,
    double Y,
    double Accuracy,
    DateTime OccurredAtUtc);
