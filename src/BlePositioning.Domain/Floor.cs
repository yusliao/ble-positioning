namespace BlePositioning.Domain;

public class Floor
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string BuildingCode { get; private set; } = null!;
    public double WidthMeters { get; private set; }
    public double HeightMeters { get; private set; }
    public string? MapImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<Beacon> Beacons { get; private set; } = new List<Beacon>();

    private Floor()
    {
    }

    public static Floor Create(string name, string buildingCode, double widthMeters, double heightMeters)
    {
        var now = DateTime.UtcNow;
        return new Floor
        {
            Id = Guid.NewGuid(),
            Name = name,
            BuildingCode = buildingCode,
            WidthMeters = widthMeters,
            HeightMeters = heightMeters,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(string name, string buildingCode, double widthMeters, double heightMeters)
    {
        Name = name;
        BuildingCode = buildingCode;
        WidthMeters = widthMeters;
        HeightMeters = heightMeters;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMapImageUrl(string? url)
    {
        MapImageUrl = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public Beacon AddBeacon(string uuid, int major, int minor, double x, double y, int txPower = -59)
    {
        var beacon = Beacon.Create(Id, uuid, major, minor, x, y, txPower);
        Beacons.Add(beacon);
        return beacon;
    }
}
