namespace BlePositioning.Application.Floors;

public sealed record UpdateAlertRuleRequest(
    string Name,
    string ZonePolygon,
    short TriggerOn,
    bool IsEnabled);
