namespace BlePositioning.Application.Positioning;

public sealed record CachedPosition(Guid FloorId, double X, double Y, double Accuracy, DateTime Timestamp);
