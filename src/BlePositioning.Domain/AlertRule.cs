namespace BlePositioning.Domain;

public class AlertRule
{
    public Guid Id { get; private set; }
    public Guid FloorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string ZonePolygon { get; private set; } = null!;
    public short TriggerOn { get; private set; }
    public bool IsEnabled { get; private set; }

    private AlertRule()
    {
    }

    public static AlertRule Create(
        Guid floorId,
        string name,
        string zonePolygon,
        short triggerOn,
        bool isEnabled = true)
    {
        return new AlertRule
        {
            Id = Guid.NewGuid(),
            FloorId = floorId,
            Name = name,
            ZonePolygon = zonePolygon,
            TriggerOn = triggerOn,
            IsEnabled = isEnabled,
        };
    }

    public void Update(string name, string zonePolygon, short triggerOn, bool isEnabled)
    {
        Name = name;
        ZonePolygon = zonePolygon;
        TriggerOn = triggerOn;
        IsEnabled = isEnabled;
    }
}
