namespace BlePositioning.Application.Floors;

public record BeaconListItemDto(
    Guid Id,
    string Uuid,
    int Major,
    int Minor,
    double X,
    double Y,
    int TxPower,
    int Status);
