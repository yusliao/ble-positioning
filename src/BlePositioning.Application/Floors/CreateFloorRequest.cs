namespace BlePositioning.Application.Floors;

public record CreateFloorRequest(string Name, string BuildingCode, double WidthMeters, double HeightMeters);
