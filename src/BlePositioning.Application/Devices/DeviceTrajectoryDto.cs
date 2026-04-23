namespace BlePositioning.Application.Devices;

public record DeviceTrajectoryDto(Guid DeviceId, int TotalPoints, IReadOnlyList<TrajectoryPointDto> Points);
