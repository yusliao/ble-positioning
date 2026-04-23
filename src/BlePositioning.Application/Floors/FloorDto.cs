namespace BlePositioning.Application.Floors;

public record FloorDto(Guid Id, string Name, string BuildingCode, double WidthMeters, double HeightMeters, string? MapImageUrl);
