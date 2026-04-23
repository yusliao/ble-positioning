namespace BlePositioning.Application.Floors;

public sealed record CreateAlertRuleRequest(
    string Name,
    string ZonePolygon,
    short TriggerOn,
    bool IsEnabled = true);
