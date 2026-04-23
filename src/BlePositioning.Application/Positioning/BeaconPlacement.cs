namespace BlePositioning.Application.Positioning;

public sealed record BeaconPlacement(
    string Uuid,
    int Major,
    int Minor,
    double X,
    double Y,
    int TxPower,
    Guid FloorId,
    double FloorWidthMeters,
    double FloorHeightMeters);
