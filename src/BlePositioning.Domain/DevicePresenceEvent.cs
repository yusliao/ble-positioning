namespace BlePositioning.Domain;

public class DevicePresenceEvent
{
    public const short KindOnline = 0;
    public const short KindOffline = 1;

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public short EventKind { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private DevicePresenceEvent()
    {
    }

    public static DevicePresenceEvent Create(Guid deviceId, short eventKind, DateTime occurredAtUtc)
    {
        if (eventKind is not (KindOnline or KindOffline))
            throw new ArgumentOutOfRangeException(nameof(eventKind));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("expected UTC", nameof(occurredAtUtc));

        return new DevicePresenceEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventKind = eventKind,
            OccurredAtUtc = occurredAtUtc,
        };
    }
}
