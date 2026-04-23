namespace BlePositioning.Application.Devices;

public sealed record DevicePresenceEventDto(Guid Id, Guid DeviceId, short EventKind, DateTime OccurredAtUtc);
