namespace BlePositioning.Application.Floors;

public record CreateBeaconRequest(
    string Uuid,
    int Major,
    int Minor,
    double X,
    double Y,
    int TxPower = -59);
