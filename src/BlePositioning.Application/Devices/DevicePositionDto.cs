namespace BlePositioning.Application.Devices;

public record DevicePositionDto(
    Guid DeviceId,
    Guid FloorId,
    double X,
    double Y,
    double Accuracy,
    DateTime Timestamp,
    bool IsOnline);
