namespace BlePositioning.Domain;

public class GeofenceEvent
{
    public const short KindEnter = 0;
    public const short KindExit = 1;

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid FloorId { get; private set; }
    public Guid AlertRuleId { get; private set; }
    public short EventKind { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Accuracy { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private GeofenceEvent()
    {
    }

    public static GeofenceEvent Create(
        Guid deviceId,
        Guid floorId,
        Guid alertRuleId,
        short eventKind,
        double x,
        double y,
        double accuracy,
        DateTime occurredAtUtc)
    {
        if (eventKind is not (KindEnter or KindExit))
            throw new ArgumentOutOfRangeException(nameof(eventKind));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("expected UTC", nameof(occurredAtUtc));

        return new GeofenceEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FloorId = floorId,
            AlertRuleId = alertRuleId,
            EventKind = eventKind,
            X = x,
            Y = y,
            Accuracy = accuracy,
            OccurredAtUtc = occurredAtUtc,
        };
    }
}
