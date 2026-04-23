namespace BlePositioning.Application.Devices;

public interface IDevicePresenceEventWriter
{
    Task WriteIfNeededAsync(short eventKind, Guid deviceId, DateTime occurredAtUtc, CancellationToken ct = default);
}
