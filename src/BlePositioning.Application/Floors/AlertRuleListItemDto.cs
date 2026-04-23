namespace BlePositioning.Application.Floors;

public sealed record AlertRuleListItemDto(
    Guid Id,
    Guid FloorId,
    string Name,
    string ZonePolygon,
    short TriggerOn,
    bool IsEnabled);
