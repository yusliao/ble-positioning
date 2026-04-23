namespace BlePositioning.Application.Floors;

public record UpdateFloorRequest(
    string Name,
    string BuildingCode,
    double WidthMeters,
    double HeightMeters,
    string? MapImageUrl);
