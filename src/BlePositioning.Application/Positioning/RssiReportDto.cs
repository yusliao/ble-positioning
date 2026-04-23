namespace BlePositioning.Application.Positioning;

public sealed record BeaconSignalDto(string Uuid, int Major, int Minor, int Rssi);

public sealed record RssiReportDto(Guid DeviceId, IReadOnlyList<BeaconSignalDto> Signals, DateTime Timestamp);
