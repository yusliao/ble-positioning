namespace BlePositioning.Application.Devices;

public record TrajectoryPointDto(double X, double Y, Guid FloorId, DateTime Timestamp);
