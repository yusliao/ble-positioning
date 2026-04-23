namespace BlePositioning.Infrastructure.Geofence;

/// <summary>Webhook JSON 体，字段与 <c>GeofenceEventNotification</c> 及 api-conventions §10 / §11 一致。</summary>
public sealed class GeofenceWebhookEventPayload
{
    public string SchemaVersion { get; set; } = "1.0";
    public Guid DeviceId { get; set; }
    public Guid FloorId { get; set; }
    public Guid AlertRuleId { get; set; }
    public short EventKind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
